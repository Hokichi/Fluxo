using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.CustomControls;

public sealed class BasePopupKeyboardShortcutTests
{
    [Fact]
    public void SaveShortcuts_UseCtrlSInsteadOfEnter()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo.Resources",
            "CustomControls",
            "BasePopup.cs"));

        Assert.Contains("case Key.S when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):", source);
        Assert.Contains("case Key.S when Keyboard.Modifiers == ModifierKeys.Control:", source);
        Assert.DoesNotContain("case Key.Enter when Keyboard.Modifiers == ModifierKeys.Shift:", source);
        Assert.DoesNotContain("case Key.Enter when Keyboard.Modifiers == ModifierKeys.None:", source);
    }
}
