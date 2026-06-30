using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups.Settings;

public sealed class SettingsAboutTabUpdateToastTests
{
    [Fact]
    public void CheckForUpdates_UsesSameToastPopupForUpToDateStatus()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Helper",
            "Settings",
            "SettingsUpdateCheckFlow.cs"));

        Assert.Contains("private static async Task<AppUpdateCheckResult> CheckForUpdatesWithOptionalToastAsync(", source);
        Assert.Contains("ownerPopup.ShowToastWhileAsync(\"Checking for updates\", async toast =>", source);
        Assert.Contains("await toast.UpdateMessageAsync(\"fluxo is up to date.\");", source);
        Assert.Contains("await Task.Delay(TimeSpan.FromSeconds(2));", source);
    }

    [Fact]
    public void UpToDateFallbackMessageBox_IsOnlyUsedWithoutSettingsPopupOwner()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Helper",
            "Settings",
            "SettingsUpdateCheckFlow.cs"));

        var handlerBody = ExtractMethodBody(source, "CheckForUpdatesAsync");

        Assert.Contains("case AppUpdateCheckStatus.UpToDate:", handlerBody);
        Assert.Contains("if (ownerPopup is null)", handlerBody);
        Assert.Contains("FluxoMessageBox.Show(", handlerBody);
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
