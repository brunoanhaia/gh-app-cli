using GhApp.Cli;
using GhApp.Support;

namespace GhApp.Tests;

public sealed class CliParserTests
{
    private static readonly IReadOnlySet<string> ValueFlags = new HashSet<string>(
        CliParser.CommonValueFlags,
        StringComparer.Ordinal
    )
    {
        "helper-name",
        "output",
    };

    private static readonly IReadOnlySet<string> NoBoolFlags = new HashSet<string>(
        StringComparer.Ordinal
    );

    [Fact]
    public void OperationBeforeFlags()
    {
        var parsed = CliParser.Parse(
            ["get", "--config", "x", "-appId", "1"],
            ValueFlags,
            NoBoolFlags
        );
        Assert.Equal(["get"], parsed.Positionals);
        Assert.Equal("x", parsed.Value("config"));
        Assert.Equal("1", parsed.Value("appId"));
    }

    [Fact]
    public void OperationAfterFlags()
    {
        var parsed = CliParser.Parse(["-config", "x", "get"], ValueFlags, NoBoolFlags);
        Assert.Equal(["get"], parsed.Positionals);
        Assert.Equal("x", parsed.Value("config"));
    }

    [Fact]
    public void EqualsForm()
    {
        var parsed = CliParser.Parse(["--config=x", "get"], ValueFlags, NoBoolFlags);
        Assert.Equal("x", parsed.Value("config"));
        Assert.Equal(["get"], parsed.Positionals);
    }

    [Fact]
    public void FlagValueCanLookLikeOperation()
    {
        var parsed = CliParser.Parse(["-username", "get", "get"], ValueFlags, NoBoolFlags);
        Assert.Equal("get", parsed.Value("username"));
        Assert.Equal(["get"], parsed.Positionals);
    }

    [Fact]
    public void SeparatorStopsFlagParsing()
    {
        var parsed = CliParser.Parse(["--", "-repo"], ValueFlags, NoBoolFlags);
        Assert.Equal(["-repo"], parsed.Positionals);
    }

    [Fact]
    public void HelpIsRecognized()
    {
        var parsed = CliParser.Parse(["-h", "-config", "x"], ValueFlags, NoBoolFlags);
        Assert.True(parsed.HelpRequested);
    }

    [Fact]
    public void BoolValues()
    {
        var parsed = CliParser.Parse(
            ["--verify=false"],
            ValueFlags,
            new HashSet<string>(["verify"], StringComparer.Ordinal)
        );
        Assert.False(parsed.Bool("verify"));
    }

    [Fact]
    public void UnknownFlagFails()
    {
        Assert.Throws<GhAppException>(() => CliParser.Parse(["-nope"], ValueFlags, NoBoolFlags));
    }

    [Fact]
    public void MissingValueFails()
    {
        Assert.Throws<GhAppException>(() => CliParser.Parse(["-config"], ValueFlags, NoBoolFlags));
    }
}
