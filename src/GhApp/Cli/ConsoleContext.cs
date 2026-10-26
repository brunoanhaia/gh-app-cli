namespace GhApp.Cli;

internal sealed record ConsoleContext(
    TextWriter Out,
    TextWriter Error,
    TextReader In,
    bool InputRedirected
);
