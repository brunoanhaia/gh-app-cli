using System.Globalization;

namespace GhApp.Helper;

internal sealed record HelperSpec(
    long AppId,
    long InstallationId,
    string PrivateKeyFile,
    string Username,
    string Domain
);

internal static class HelperCommand
{
    private const string UnsafeShellChars = " \t\n\r'\"\\$`&|;<>()*?[]{}!";

    public static string Build(IReadOnlyList<string> prefix, HelperSpec spec)
    {
        var parts = new List<string>();
        foreach (var part in prefix)
        {
            parts.Add(QuotePath(part));
        }
        parts.Add("credential");
        parts.Add("-appId");
        parts.Add(spec.AppId.ToString(CultureInfo.InvariantCulture));
        parts.Add("-installationId");
        parts.Add(spec.InstallationId.ToString(CultureInfo.InvariantCulture));
        parts.Add("-privateKeyFile");
        parts.Add(QuotePath(spec.PrivateKeyFile));
        if (!string.IsNullOrWhiteSpace(spec.Username))
        {
            parts.Add("-username");
            parts.Add(ShellQuote(spec.Username));
        }
        if (
            !string.IsNullOrWhiteSpace(spec.Domain)
            && !string.Equals(spec.Domain, "github.com", StringComparison.OrdinalIgnoreCase)
        )
        {
            parts.Add("-domain");
            parts.Add(ShellQuote(spec.Domain));
        }
        return "!" + string.Join(' ', parts);
    }

    internal static string ShellQuote(string value)
    {
        if (value.Length == 0)
        {
            return "''";
        }
        if (value.All(c => !UnsafeShellChars.Contains(c)))
        {
            return value;
        }
        return "'" + value.Replace("'", "'\\''") + "'";
    }

    private static string QuotePath(string value)
    {
        return ShellQuote(value.Replace('\\', '/'));
    }
}
