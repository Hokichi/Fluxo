using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups.Settings;

public sealed class SettingsAutosaveAnimationTests
{
    [Fact]
    public void ApplyConfigurationAsync_DoesNotReloadSettingsTabs_AfterAutosave()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "ViewModels",
            "Popups",
            "Settings",
            "SettingsVM.cs"));

        var methodBody = ExtractMethodBody(source, "ApplyConfigurationAsync");

        Assert.DoesNotContain("await LoadAsync();", methodBody);
    }

    private static string ExtractMethodBody(string source, string methodName)
    {
        var methodStart = source.IndexOf(methodName, StringComparison.Ordinal);
        Assert.True(methodStart >= 0, $"Could not find method {methodName}.");

        var bodyStart = source.IndexOf('{', methodStart);
        Assert.True(bodyStart >= 0, $"Could not find method body for {methodName}.");

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            depth += source[index] switch
            {
                '{' => 1,
                '}' => -1,
                _ => 0
            };

            if (depth == 0)
                return source[bodyStart..(index + 1)];
        }

        throw new InvalidOperationException($"Could not parse method body for {methodName}.");
    }
}
