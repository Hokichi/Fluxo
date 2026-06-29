using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups.Settings;

public sealed class SettingsTabCrossfadeTests
{
    private static readonly string[] TabContentNames =
    [
        "BudgetTabContent",
        "AccountsTabContent",
        "RecurringTransactionsTabContent",
        "GoalsTabContent",
        "TagsTabContent",
        "PersonalizationTabContent",
        "AboutTabContent"
    ];

    [Fact]
    public void SettingsPopup_NamesTabContentElementsForCrossfade()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Popups",
            "Settings",
            "SettingsPopup.xaml"));

        foreach (var tabContentName in TabContentNames)
            Assert.Contains($"x:Name=\"{tabContentName}\"", xaml);
    }

    [Fact]
    public void SettingsPopup_TabSelectionRunsCrossfadeAfterLeaveGuard()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Popups",
            "Settings",
            "SettingsPopup.xaml.cs"));

        var handlerBody = ExtractMethodBody(source, "OnSettingsTabPreviewMouseLeftButtonDown");

        Assert.Contains("await CanLeaveCurrentSettingsTabAsync()", handlerBody);
        Assert.Contains("await CrossfadeSettingsTabAsync(targetTab);", handlerBody);
        Assert.DoesNotContain("targetTab.IsChecked = true;", handlerBody);
    }

    [Fact]
    public void SettingsPopup_CrossfadeUsesPureOpacityAnimation()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Popups",
            "Settings",
            "SettingsPopup.xaml.cs"));

        Assert.Contains("using System.Windows.Media.Animation;", source);
        Assert.Contains("private static readonly Duration TabFadeDuration", source);
        Assert.Contains("CrossfadeSettingsTabAsync(RadioButton targetTab)", source);
        Assert.Contains("private static Task FadeElementAsync(UIElement element, double from, double to)", source);
        Assert.Contains("new DoubleAnimation(from, to, TabFadeDuration)", source);
        Assert.Contains("BeginAnimation(OpacityProperty, animation)", source);
        Assert.DoesNotContain("TranslateTransform", source);
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
