using GhApp.Settings;
using GhApp.Support;

namespace GhApp.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void ValidateAuthRejectsNewlines()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "gh-app-cs-settings-" + Guid.NewGuid().ToString("N")
        );
        try
        {
            var keyPath = TestKeys.WriteKeyFile(directory);
            var badUser = new AppSettings
            {
                AppId = 1,
                PrivateKeyFile = keyPath,
                Username = "a\nb",
            };
            Assert.Throws<GhAppException>(() => badUser.ValidateAuth());

            var badPath = new AppSettings { AppId = 1, PrivateKeyFile = keyPath + "\n" };
            Assert.Throws<GhAppException>(() => badPath.ValidateAuth());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
