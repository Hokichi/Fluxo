using System.Windows.Input;
using Fluxo.Views.Shell.Main;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public class MainWindowShortcutMatcherTests
{
    [Fact]
    public void IsRunSetupWizardShortcut_ReturnsTrue_ForCtrlW()
    {
        var isShortcut = MainWindowShortcutMatcher.IsRunSetupWizardShortcut(Key.W, ModifierKeys.Control);

        Assert.True(isShortcut);
    }

    [Theory]
    [InlineData(Key.W, ModifierKeys.None)]
    [InlineData(Key.W, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.S, ModifierKeys.Control)]
    public void IsRunSetupWizardShortcut_ReturnsFalse_ForOtherKeysOrModifiers(Key key, ModifierKeys modifiers)
    {
        var isShortcut = MainWindowShortcutMatcher.IsRunSetupWizardShortcut(key, modifiers);

        Assert.False(isShortcut);
    }

    [Fact]
    public void IsOpenPlanningShortcut_ReturnsTrue_ForCtrlP()
    {
        var isShortcut = MainWindowShortcutMatcher.IsOpenPlanningShortcut(Key.P, ModifierKeys.Control);

        Assert.True(isShortcut);
    }

    [Theory]
    [InlineData(Key.P, ModifierKeys.None)]
    [InlineData(Key.P, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.O, ModifierKeys.Control)]
    public void IsOpenPlanningShortcut_ReturnsFalse_ForOtherKeysOrModifiers(Key key, ModifierKeys modifiers)
    {
        var isShortcut = MainWindowShortcutMatcher.IsOpenPlanningShortcut(key, modifiers);

        Assert.False(isShortcut);
    }

    [Fact]
    public void IsOpenAnalyticsShortcut_ReturnsTrue_ForCtrlA()
    {
        var isShortcut = MainWindowShortcutMatcher.IsOpenAnalyticsShortcut(Key.A, ModifierKeys.Control);

        Assert.True(isShortcut);
    }

    [Theory]
    [InlineData(Key.A, ModifierKeys.None)]
    [InlineData(Key.A, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.D, ModifierKeys.Control)]
    public void IsOpenAnalyticsShortcut_ReturnsFalse_ForOtherKeysOrModifiers(Key key, ModifierKeys modifiers)
    {
        var isShortcut = MainWindowShortcutMatcher.IsOpenAnalyticsShortcut(key, modifiers);

        Assert.False(isShortcut);
    }
}
