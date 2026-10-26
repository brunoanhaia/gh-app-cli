using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using GhApp.Config;
using GhApp.Credential;
using GhApp.Services;
using GhApp.Settings;
using GhApp.Support;

namespace GhApp.Cli;

internal sealed class CommandDispatcher
{
    private readonly TextWriter _out;
    private readonly TextWriter _error;
    private readonly TextReader _in;
    private readonly bool _inputRedirected;
    private readonly IGhAppService _service;

    public CommandDispatcher(ConsoleContext console, IGhAppService service)
    {
        _out = console.Out;
        _error = console.Error;
        _in = console.In;
        _inputRedirected = console.InputRedirected;
        _service = service;
    }

    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            _error.Write(HelpText.General);
            return 1;
        }

        try
        {
            switch (args[0])
            {
                case "help":
                case "-h":
                case "--help":
                    _out.Write(HelpText.General);
                    return 0;
                case "version":
                case "-version":
                case "--version":
                case "-v":
                    return await RunVersionAsync(args.Skip(1).ToArray());
                case "identity":
                    return await RunIdentityAsync(args.Skip(1).ToArray());
                case "token":
                    return await RunTokenAsync(args.Skip(1).ToArray());
                case "credential":
                    return await RunCredentialAsync(args.Skip(1).ToArray(), helperMode: false);
                case "get":
                case "store":
                case "erase":
                    // Git appends the operation to a custom helper command.
                    return await RunCredentialAsync(args, helperMode: true);
                case "generate":
                    return await RunGenerateAsync(args.Skip(1).ToArray());
                case "setup":
                    return await RunSetupAsync(args.Skip(1).ToArray());
            }

            if (args[0].StartsWith("-", StringComparison.Ordinal))
            {
                return await RunCredentialAsync(args, helperMode: true);
            }

            if (_inputRedirected)
            {
                // Git invokes a helper with an operation argument and a pipe on
                // stdin. Unknown operations must be ignored for compatibility.
                return await RunCredentialAsync(args, helperMode: true);
            }

            _error.WriteLine($"unknown command: {args[0]}");
            _error.WriteLine();
            _error.Write(HelpText.General);
            return 1;
        }
        catch (GhAppException ex)
        {
            _error.WriteLine(ex.Message);
            return 1;
        }
        catch (HttpRequestException ex)
        {
            _error.WriteLine($"network error: {ex.Message}");
            return 1;
        }
        catch (OperationCanceledException)
        {
            _error.WriteLine("operation canceled or timed out");
            return 1;
        }
        catch (Exception ex)
        {
            _error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private Task<int> RunVersionAsync(string[] args)
    {
        var parsed = CliParser.Parse(args, NoValueFlags(), BoolFlags("verbose"));
        if (parsed.HelpRequested)
        {
            _out.Write(HelpText.Version);
            return Task.FromResult(0);
        }
        if (parsed.Positionals.Count != 0)
        {
            _error.WriteLine("usage: gh-app version [--verbose]");
            return Task.FromResult(1);
        }

        _out.WriteLine($"gh-app {VersionInfo.Version}");
        if (parsed.Bool("verbose"))
        {
            _out.WriteLine($"dotnet\t{VersionInfo.Framework}");
            _out.WriteLine($"os\t{VersionInfo.OS}");
            _out.WriteLine($"arch\t{VersionInfo.Architecture}");
        }
        return Task.FromResult(0);
    }

    private async Task<int> RunIdentityAsync(string[] args)
    {
        var parsed = CliParser.Parse(args, ValueFlags(), BoolFlags("verify"));
        if (parsed.HelpRequested)
        {
            _out.Write(HelpText.Identity);
            return 0;
        }
        if (parsed.Positionals.Count != 0)
        {
            _error.WriteLine("usage: gh-app identity [--verify] [flags]");
            return 1;
        }

        var settings = Resolve(parsed);
        settings.ValidateAuth();
        var verify = parsed.Bool("verify");
        if (verify)
        {
            settings.ValidateInstallation();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var identity = await _service.GetIdentityAsync(settings, verify, cts.Token);
        var json = JsonSerializer.Serialize(
            identity,
            new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }
        );
        _out.Write(json);
        _out.Flush();
        return 0;
    }

    private async Task<int> RunTokenAsync(string[] args)
    {
        var parsed = CliParser.Parse(args, ValueFlags(), NoValueFlags());
        if (parsed.HelpRequested)
        {
            _out.Write(HelpText.Token);
            return 0;
        }
        if (parsed.Positionals.Count != 0)
        {
            _error.WriteLine("usage: gh-app token [flags]");
            return 1;
        }

        var settings = Resolve(parsed);
        settings.ValidateAuth();
        settings.ValidateInstallation();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = await _service.GetTokenAsync(settings, cts.Token);
        _out.Write(token.Token);
        _out.Flush();
        return 0;
    }

    private async Task<int> RunCredentialAsync(string[] args, bool helperMode)
    {
        var parsed = CliParser.Parse(args, ValueFlags("helper-name", "output"), NoValueFlags());
        if (parsed.HelpRequested)
        {
            _out.Write(HelpText.Credential);
            return 0;
        }
        if (parsed.Positionals.Count != 1)
        {
            if (helperMode)
            {
                return 0;
            }
            _error.WriteLine("usage: gh-app credential [flags] get|store|erase|generate");
            return 1;
        }

        switch (parsed.Positionals[0])
        {
            case "store":
            case "erase":
                if (_inputRedirected)
                {
                    // Drain the request so Git does not receive EPIPE.
                    _in.ReadToEnd();
                }
                return 0;
            case "generate":
                if (helperMode)
                {
                    return 0;
                }
                return await GenerateCoreAsync(parsed);
            case "get":
                return await CredentialGetAsync(parsed);
            default:
                if (helperMode)
                {
                    // Git custom helpers must ignore unsupported operations.
                    return 0;
                }
                _error.WriteLine($"unknown operation: {parsed.Positionals[0]}");
                return 1;
        }
    }

    private async Task<int> CredentialGetAsync(ParsedArgs parsed)
    {
        try
        {
            CredentialRequest? request = _inputRedirected ? CredentialProtocol.Parse(_in) : null;
            var settings = Resolve(parsed);
            if (request is not null && !CredentialProtocol.Allowed(request, settings.Domain))
            {
                return 0;
            }

            settings.ValidateAuth();
            settings.ValidateInstallation();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var token = await _service.GetTokenAsync(settings, cts.Token);
            CredentialProtocol.WriteGet(_out, settings.Username, token.Token, token.ExpiresAt);
            return 0;
        }
        catch (GhAppException ex)
        {
            CredentialProtocol.WriteQuit(_out);
            _error.WriteLine(ex.Message);
            return 1;
        }
        catch (HttpRequestException ex)
        {
            CredentialProtocol.WriteQuit(_out);
            _error.WriteLine($"network error: {ex.Message}");
            return 1;
        }
        catch (OperationCanceledException)
        {
            CredentialProtocol.WriteQuit(_out);
            _error.WriteLine("operation canceled or timed out");
            return 1;
        }
        catch (Exception ex)
        {
            CredentialProtocol.WriteQuit(_out);
            _error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private async Task<int> RunGenerateAsync(string[] args)
    {
        var parsed = CliParser.Parse(args, ValueFlags("helper-name", "output"), NoValueFlags());
        if (parsed.HelpRequested)
        {
            _out.Write(HelpText.Generate);
            return 0;
        }
        if (parsed.Positionals.Count != 0)
        {
            _error.WriteLine("usage: gh-app generate [flags]");
            return 1;
        }
        return await GenerateCoreAsync(parsed);
    }

    private async Task<int> GenerateCoreAsync(ParsedArgs parsed)
    {
        var settings = Resolve(parsed);
        settings.ValidateAuth();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var helperName = parsed.IsSet("helper-name") ? parsed.Value("helper-name") : null;
        var generated = await _service.GenerateConfigAsync(settings, helperName, cts.Token);

        var output = (parsed.Value("output") ?? "").Trim();
        if (output.Length == 0)
        {
            _out.Write(generated.Content);
            _out.Flush();
            return 0;
        }

        var path = Path.GetFullPath(PathExpander.ExpandHome(output));
        try
        {
            File.WriteAllText(path, generated.Content, new UTF8Encoding(false));
        }
        catch (Exception ex)
            when (ex
                    is IOException
                        or UnauthorizedAccessException
                        or ArgumentException
                        or NotSupportedException
            )
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // best effort
            }
            throw new GhAppException($"could not write {path}: {ex.Message}", ex);
        }

        _error.WriteLine($"config written to {path}");
        return 0;
    }

    private async Task<int> RunSetupAsync(string[] args)
    {
        var parsed = CliParser.Parse(args, ValueFlags(), NoValueFlags());
        if (parsed.HelpRequested)
        {
            _out.Write(HelpText.Setup);
            return 0;
        }
        if (parsed.Positionals.Count != 1)
        {
            _error.WriteLine("usage: gh-app setup [flags] <repo>");
            return 1;
        }

        var settings = Resolve(parsed);
        settings.ValidateAuth();
        settings.ValidateInstallation();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var result = await _service.SetupRepositoryAsync(
            settings,
            parsed.Positionals[0],
            cts.Token
        );
        WriteSetupSummary(result);
        return 0;
    }

    private void WriteSetupSummary(SetupResult result)
    {
        _out.WriteLine();
        _out.WriteLine($"Repository configured: {result.Repository}");
        _out.WriteLine($"  app       : {result.Identity.Slug}");
        _out.WriteLine($"  user.name : {result.Identity.Login}");
        _out.WriteLine($"  user.email: {result.Identity.Email}");
        _out.WriteLine($"  repos     : {result.TotalRepositories} visible to the installation");
        _out.WriteLine($"  helper    : {result.Helper}");
        _out.WriteLine();
        _out.WriteLine("Test:");
        _out.WriteLine($"  git -C \"{result.Repository}\" commit --allow-empty -m \"bot test\"");
        _out.WriteLine(
            $"  git -C \"{result.Repository}\" log -1 --format=\"%an <%ae>%n%cn <%ce>%\""
        );
        _out.WriteLine($"  git -C \"{result.Repository}\" push");
    }

    private static AppSettings Resolve(ParsedArgs parsed)
    {
        return SettingsResolver.Resolve(parsed);
    }

    private static HashSet<string> ValueFlags(params string[] extra)
    {
        var set = new HashSet<string>(CliParser.CommonValueFlags, StringComparer.Ordinal);
        foreach (var name in extra)
        {
            set.Add(name);
        }
        return set;
    }

    private static HashSet<string> BoolFlags(params string[] names)
    {
        return new HashSet<string>(names, StringComparer.Ordinal);
    }

    private static HashSet<string> NoValueFlags()
    {
        return new HashSet<string>(StringComparer.Ordinal);
    }
}
