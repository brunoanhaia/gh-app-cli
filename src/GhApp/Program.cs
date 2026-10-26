using GhApp.Cli;
using GhApp.Git;
using GhApp.GitHub;
using GhApp.Helper;
using GhApp.Services;
using GhApp.Setup;
using Microsoft.Extensions.DependencyInjection;

namespace GhApp;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddSingleton(
            new ConsoleContext(Console.Out, Console.Error, Console.In, Console.IsInputRedirected)
        );
        services.AddSingleton<IGitRunner, GitRunner>();
        services.AddSingleton<IRepositorySetup, RepositorySetup>();
        services.AddSingleton<IExecutableResolver, ExecutableResolver>();
        services.AddSingleton<IGitHubAppClientFactory, GitHubAppClientFactory>();
        services.AddSingleton<IGhAppService, GhAppService>();
        services.AddTransient<CommandDispatcher>();

        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<CommandDispatcher>();
        return await dispatcher.RunAsync(args);
    }
}
