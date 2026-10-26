namespace GhApp.Helper;

internal interface IExecutableResolver
{
    IReadOnlyList<string> DefaultPrefix();

    IReadOnlyList<string> FromHelperName(string? helperName);
}
