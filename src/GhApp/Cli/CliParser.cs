using GhApp.Support;

namespace GhApp.Cli;

internal sealed class ParsedArgs
{
    private readonly Dictionary<string, string?> _values;

    public ParsedArgs(
        Dictionary<string, string?> values,
        IReadOnlyList<string> positionals,
        bool helpRequested
    )
    {
        _values = values;
        Positionals = positionals;
        HelpRequested = helpRequested;
    }

    public IReadOnlyList<string> Positionals { get; }
    public bool HelpRequested { get; }

    public bool IsSet(string name)
    {
        return _values.ContainsKey(name);
    }

    public string? Value(string name)
    {
        return _values.TryGetValue(name, out var value) ? value : null;
    }

    public bool Bool(string name)
    {
        return IsSet(name)
            && string.Equals(Value(name), "true", StringComparison.OrdinalIgnoreCase);
    }
}

internal static class CliParser
{
    public static readonly IReadOnlySet<string> CommonValueFlags = new HashSet<string>(
        StringComparer.Ordinal
    )
    {
        "config",
        "appId",
        "installationId",
        "organization",
        "privateKeyFile",
        "username",
        "domain",
    };

    public static ParsedArgs Parse(
        IReadOnlyList<string> args,
        IReadOnlySet<string> valueFlags,
        IReadOnlySet<string> boolFlags
    )
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        var positionals = new List<string>();
        var helpRequested = false;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument == "--")
            {
                positionals.AddRange(args.Skip(index + 1));
                break;
            }
            if (argument.Length > 1 && argument[0] == '-')
            {
                var raw = argument.TrimStart('-');
                if (raw.Length == 0)
                {
                    throw new GhAppException($"invalid argument: {argument}");
                }

                var equals = raw.IndexOf('=');
                var name = equals >= 0 ? raw[..equals] : raw;
                var inlineValue = equals >= 0 ? raw[(equals + 1)..] : null;

                if (name is "h" or "help")
                {
                    helpRequested = true;
                    break;
                }

                if (boolFlags.Contains(name))
                {
                    var value = inlineValue ?? "true";
                    if (
                        !string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        throw new GhAppException($"invalid value for -{name}: {value}");
                    }
                    values[name] = value.ToLowerInvariant();
                    continue;
                }

                if (valueFlags.Contains(name))
                {
                    string value;
                    if (inlineValue is not null)
                    {
                        value = inlineValue;
                    }
                    else if (index + 1 < args.Count)
                    {
                        value = args[++index];
                    }
                    else
                    {
                        throw new GhAppException($"flag -{name} requires a value");
                    }
                    values[name] = value;
                    continue;
                }

                throw new GhAppException($"unknown flag: -{name}");
            }

            positionals.Add(argument);
        }

        return new ParsedArgs(values, positionals, helpRequested);
    }
}
