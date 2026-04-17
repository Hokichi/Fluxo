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
}
