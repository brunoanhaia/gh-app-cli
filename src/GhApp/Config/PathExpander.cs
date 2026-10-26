namespace GhApp.Config;

internal static class PathExpander
{
    public static string ExpandHome(string path)
    {
        path = path.Trim();
        if (path.Length == 0)
        {
            return path;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length == 0)
        {
            return path;
        }
        if (path == "~")
        {
            return home;
        }
        if (
            path.StartsWith("~/", StringComparison.Ordinal)
            || path.StartsWith("~\\", StringComparison.Ordinal)
        )
        {
            return Path.Combine(home, path[2..]);
        }
        return path;
    }
}
