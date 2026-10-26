using System.Reflection;
using System.Runtime.InteropServices;

namespace GhApp.Cli;

internal static class VersionInfo
{
    public static string Version
    {
        get
        {
            var attribute =
                typeof(VersionInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var value = attribute?.InformationalVersion ?? "dev";
            var plus = value.IndexOf('+');
            return plus >= 0 ? value[..plus] : value;
        }
    }

    public static string Framework => RuntimeInformation.FrameworkDescription;
    public static string OS => RuntimeInformation.OSDescription;
    public static string Architecture => RuntimeInformation.ProcessArchitecture.ToString();
}
