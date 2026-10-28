using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using GhApp.Git;
using GhApp.Support;

namespace GhApp.Credential;

internal sealed record CachedCredential(string Token, DateTimeOffset? ExpiresAt);

internal interface IGitCredentialCache
{
    Task<CachedCredential?> GetAsync(
        string domain,
        string key,
        CancellationToken cancellationToken = default
    );

    Task StoreAsync(
        string domain,
        string key,
        string username,
        string token,
        DateTimeOffset expires,
        CancellationToken cancellationToken = default
    );
}

internal sealed class GitCredentialCache : IGitCredentialCache
{
    private const int TimeoutSeconds = 3300;
    private readonly string? _socketPath;

    public GitCredentialCache(string? socketPath = null)
    {
        _socketPath = string.IsNullOrWhiteSpace(socketPath) ? null : socketPath;
    }

    public async Task<CachedCredential?> GetAsync(
        string domain,
        string key,
        CancellationToken cancellationToken = default
    )
    {
        var request = $"protocol=https\nhost={domain}\npath={key}\n\n";
        var result = await RunAsync("get", request, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new GhAppException(
                $"git credential-cache get failed: {Detail(result.StandardError, result.StandardOutput)}"
            );
        }

        return ParseGet(result.StandardOutput);
    }

    public async Task StoreAsync(
        string domain,
        string key,
        string username,
        string token,
        DateTimeOffset expires,
        CancellationToken cancellationToken = default
    )
    {
        var request =
            $"protocol=https\nhost={domain}\npath={key}\nusername={username}\npassword={token}\npassword_expiry_utc={expires.ToUnixTimeSeconds()}\n\n";
        var result = await RunAsync("store", request, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new GhAppException(
                $"git credential-cache store failed: {Detail(result.StandardError, result.StandardOutput)}"
            );
        }
    }

    internal static CachedCredential? ParseGet(string output)
    {
        string? token = null;
        DateTimeOffset? expires = null;
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var separator = line.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }

            var name = line[..separator];
            var value = line[(separator + 1)..];
            if (name == "password")
            {
                token = value;
            }
            else if (
                name == "password_expiry_utc"
                && long.TryParse(value, out var unixSeconds)
                && unixSeconds > 0
            )
            {
                expires = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            }
        }

        return string.IsNullOrEmpty(token) ? null : new CachedCredential(token, expires);
    }

    private async Task<GitResult> RunAsync(
        string action,
        string input,
        CancellationToken cancellationToken
    )
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // A BOM would become part of the first protocol line and break parsing.
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("credential-cache");
        startInfo.ArgumentList.Add($"--timeout={TimeoutSeconds}");
        if (_socketPath is not null)
        {
            startInfo.ArgumentList.Add($"--socket={_socketPath}");
        }
        startInfo.ArgumentList.Add(action);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new GhAppException("could not start git credential-cache");
            }
        }
        catch (Win32Exception ex)
        {
            throw new GhAppException("git not found in PATH", ex);
        }

        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // best effort
            }
            throw;
        }

        return new GitResult(process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static string Detail(string stderr, string stdout)
    {
        var detail = stderr.Trim();
        return detail.Length == 0 ? stdout.Trim() : detail;
    }
}
