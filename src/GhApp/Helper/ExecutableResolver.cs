using System.Reflection;

namespace GhApp.Helper;

internal sealed class ExecutableResolver : IExecutableResolver
{
    public IReadOnlyList<string> DefaultPrefix()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var resolved = ResolveSymlink(processPath);
            var fileName = Path.GetFileName(resolved);
            if (
                string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "dotnet.exe", StringComparison.OrdinalIgnoreCase)
            )
            {
                var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    var assemblyPath = Path.Combine(
                        AppContext.BaseDirectory,
                        assemblyName + ".dll"
                    );
                    return [resolved, assemblyPath];
                }
            }
            return [resolved];
        }

        var baseDirectory = AppContext.BaseDirectory;
        var binaryName = OperatingSystem.IsWindows() ? "gh-app.exe" : "gh-app";
        return [Path.Combine(baseDirectory, binaryName)];
    }

    public IReadOnlyList<string> FromHelperName(string? helperName)
    {
        helperName = helperName?.Trim() ?? "";
        if (helperName.Length == 0)
        {
            return DefaultPrefix();
        }
        return [helperName];
    }

    private static string ResolveSymlink(string path)
    {
        try
        {
            var target = new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true);
            return target?.FullName ?? path;
        }
        catch
        {
            return path;
        }
    }
}
