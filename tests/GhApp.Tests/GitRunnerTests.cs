using GhApp.Git;
using GhApp.Support;

namespace GhApp.Tests;

public sealed class GitRunnerTests
{
    private readonly GitRunner _git = new();

    [Fact]
    public async Task PreparesAndConfiguresRepository()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = NewDirectory("git");
        try
        {
            await _git.RunCheckedAsync(repository, ["init", "-q"], cancellationToken);
            var prepared = await _git.PrepareRepoAsync(repository, cancellationToken);
            Assert.Equal(Path.GetFullPath(repository), prepared);

            await _git.SetConfigAsync(repository, "user.name", "bot", cancellationToken);
            Assert.Contains(
                "bot",
                await _git.GetAllConfigAsync(repository, "user.name", cancellationToken)
            );

            await _git.ReplaceAllConfigAsync(
                repository,
                "credential.helper",
                "",
                cancellationToken
            );
            await _git.AddConfigAsync(repository, "credential.helper", "helper", cancellationToken);
            var helpers = await _git.GetAllConfigAsync(
                repository,
                "credential.helper",
                cancellationToken
            );
            Assert.Equal("", helpers[0]);
            Assert.Contains("helper", helpers);

            await _git.RunCheckedAsync(
                repository,
                ["remote", "add", "origin", "https://github.com/acme/repo.git"],
                cancellationToken
            );
            Assert.Equal(
                "https://github.com/acme/repo.git",
                await _git.GetRemoteUrlAsync(repository, "origin", cancellationToken)
            );
        }
        finally
        {
            Directory.Delete(repository, true);
        }
    }

    [Fact]
    public async Task NonRepositoryFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = NewDirectory("not-repo");
        try
        {
            await Assert.ThrowsAsync<GhAppException>(() =>
                _git.PrepareRepoAsync(directory, cancellationToken)
            );
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string NewDirectory(string prefix)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"gh-app-cs-{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
