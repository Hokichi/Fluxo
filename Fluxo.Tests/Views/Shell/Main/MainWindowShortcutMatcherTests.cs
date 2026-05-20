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
    public void IsOpenAnalyticsShortcut_ReturnsTrue_ForCtrlShiftA()
    {
        var isShortcut = MainWindowShortcutMatcher.IsOpenAnalyticsShortcut(
            Key.A,
            ModifierKeys.Control | ModifierKeys.Shift);

        Assert.True(isShortcut);
    }

    [Theory]
    [InlineData(Key.A, ModifierKeys.None)]
    [InlineData(Key.A, ModifierKeys.Control)]
    [InlineData(Key.D, ModifierKeys.Control)]
    public void IsOpenAnalyticsShortcut_ReturnsFalse_ForOtherKeysOrModifiers(Key key, ModifierKeys modifiers)
    {
        var isShortcut = MainWindowShortcutMatcher.IsOpenAnalyticsShortcut(key, modifiers);

        Assert.False(isShortcut);
    }

    [Fact]
    public void IsCloseAnalyticsShortcut_ReturnsTrue_ForEscape()
    {
        var isShortcut = MainWindowShortcutMatcher.IsCloseAnalyticsShortcut(Key.Escape, ModifierKeys.None);

        Assert.True(isShortcut);
    }

    [Theory]
    [InlineData(Key.Escape, ModifierKeys.Control)]
    [InlineData(Key.Escape, ModifierKeys.Shift)]
    [InlineData(Key.A, ModifierKeys.None)]
    public void IsCloseAnalyticsShortcut_ReturnsFalse_ForOtherKeysOrModifiers(Key key, ModifierKeys modifiers)
    {
        var isShortcut = MainWindowShortcutMatcher.IsCloseAnalyticsShortcut(key, modifiers);

        Assert.False(isShortcut);
    }

    [Fact]
    public void IsOpenSearchShortcut_ReturnsTrue_ForCtrlF()
    {
        var isShortcut = MainWindowShortcutMatcher.IsOpenSearchShortcut(Key.F, ModifierKeys.Control);

        Assert.True(isShortcut);
    }

    [Theory]
    [InlineData(Key.F, ModifierKeys.None)]
    [InlineData(Key.F, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.S, ModifierKeys.Control)]
    public void IsOpenSearchShortcut_ReturnsFalse_ForOtherKeysOrModifiers(Key key, ModifierKeys modifiers)
    {
        var isShortcut = MainWindowShortcutMatcher.IsOpenSearchShortcut(key, modifiers);

        Assert.False(isShortcut);
    }

    [Fact]
    public void IsOpenQuickAddShortcut_ReturnsTrue_ForCtrlN()
    {
        var isShortcut = MainWindowShortcutMatcher.IsOpenQuickAddShortcut(Key.N, ModifierKeys.Control);

        Assert.True(isShortcut);
    }

    [Theory]
    [InlineData(Key.N, ModifierKeys.None)]
    [InlineData(Key.N, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.M, ModifierKeys.Control)]
    public void IsOpenQuickAddShortcut_ReturnsFalse_ForOtherKeysOrModifiers(Key key, ModifierKeys modifiers)
    {
        var isShortcut = MainWindowShortcutMatcher.IsOpenQuickAddShortcut(key, modifiers);

        Assert.False(isShortcut);
    }
}
