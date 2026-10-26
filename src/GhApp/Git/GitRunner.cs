using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using GhApp.Support;

namespace GhApp.Git;

internal sealed record GitResult(int ExitCode, string StandardOutput, string StandardError);

internal sealed class GitRunner : IGitRunner
{
    public async Task<GitResult> RunAsync(
        string? repository,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default
    )
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (repository is not null)
        {
            startInfo.ArgumentList.Add("-C");
            startInfo.ArgumentList.Add(repository);
        }
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new GhAppException("could not start git");
            }
        }
        catch (Win32Exception ex)
        {
            throw new GhAppException("git not found in PATH", ex);
        }

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

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return new GitResult(process.ExitCode, stdout, stderr);
    }

    public async Task<string> RunCheckedAsync(
        string? repository,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default
    )
    {
        var result = await RunAsync(repository, args, cancellationToken);
        if (result.ExitCode != 0)
        {
            var detail = result.StandardError.Trim();
            if (detail.Length == 0)
            {
                detail = result.StandardOutput.Trim();
            }
            throw new GhAppException($"git {string.Join(' ', args)} failed: {detail}");
        }
        return result.StandardOutput.TrimEnd('\r', '\n');
    }

    public async Task<string> PrepareRepoAsync(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        var fullPath = Path.GetFullPath(path);
        var result = await RunAsync(fullPath, ["rev-parse", "--git-dir"], cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new GhAppException($"not a git repository: {fullPath}");
        }
        return fullPath;
    }

    public async Task<string> GetRemoteUrlAsync(
        string repository,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        return await RunCheckedAsync(repository, ["remote", "get-url", name], cancellationToken);
    }

    public async Task SetConfigAsync(
        string repository,
        string key,
        string value,
        CancellationToken cancellationToken = default
    )
    {
        var result = await RunAsync(
            repository,
            ["config", "--local", "--replace-all", key, value],
            cancellationToken
        );
        if (result.ExitCode != 0)
        {
            throw new GhAppException($"git config {key} failed: {result.StandardError.Trim()}");
        }
    }

    public async Task ReplaceAllConfigAsync(
        string repository,
        string key,
        string value,
        CancellationToken cancellationToken = default
    )
    {
        var unset = await RunAsync(
            repository,
            ["config", "--local", "--unset-all", key],
            cancellationToken
        );
        // 5 = missing key; any other error is real.
        if (unset.ExitCode is not (0 or 5))
        {
            throw new GhAppException(
                $"git config --unset-all {key} failed: {unset.StandardError.Trim()}"
            );
        }
        var add = await RunAsync(
            repository,
            ["config", "--local", "--add", key, value],
            cancellationToken
        );
        if (add.ExitCode != 0)
        {
            throw new GhAppException($"git config --add {key} failed: {add.StandardError.Trim()}");
        }
    }

    public async Task AddConfigAsync(
        string repository,
        string key,
        string value,
        CancellationToken cancellationToken = default
    )
    {
        var result = await RunAsync(
            repository,
            ["config", "--local", "--add", key, value],
            cancellationToken
        );
        if (result.ExitCode != 0)
        {
            throw new GhAppException(
                $"git config --add {key} failed: {result.StandardError.Trim()}"
            );
        }
    }

    public async Task<IReadOnlyList<string>> GetAllConfigAsync(
        string repository,
        string key,
        CancellationToken cancellationToken = default
    )
    {
        var result = await RunAsync(
            repository,
            ["config", "--local", "--get-all", key],
            cancellationToken
        );
        if (result.ExitCode == 1)
        {
            return [];
        }
        if (result.ExitCode != 0)
        {
            throw new GhAppException(
                $"git config --get-all {key} failed: {result.StandardError.Trim()}"
            );
        }
        return result
            .StandardOutput.Split('\n', StringSplitOptions.None)
            .Select(line => line.TrimEnd('\r'))
            .ToList();
    }
}
