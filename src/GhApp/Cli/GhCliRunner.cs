using System.ComponentModel;
using System.Diagnostics;
using GhApp.Settings;
using GhApp.Support;

namespace GhApp.Cli;

internal interface IGhCliRunner
{
    Task<int> RunAsync(
        AppSettings settings,
        string token,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default
    );
}

internal sealed class GhCliRunner : IGhCliRunner
{
    public async Task<int> RunAsync(
        AppSettings settings,
        string token,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default
    )
    {
        var startInfo = new ProcessStartInfo("gh") { UseShellExecute = false };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        foreach (
            var name in new[]
            {
                "GH_TOKEN",
                "GITHUB_TOKEN",
                "GH_ENTERPRISE_TOKEN",
                "GITHUB_ENTERPRISE_TOKEN",
                "GH_HOST",
            }
        )
        {
            startInfo.Environment.Remove(name);
        }
        foreach (var pair in GhCliEnvironment.Build(settings.Domain, token))
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new GhAppException("could not start gh");
            }
        }
        catch (Win32Exception ex)
        {
            throw new GhAppException("GitHub CLI (gh) not found in PATH", ex);
        }

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
