using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class TransactionDetailPopupKeyboardTests
{
    [Fact]
    public void CtrlD_ClonesTransactionInDetailPopup()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Popups",
            "TransactionDetailPopup.xaml.cs"));

        Assert.Contains("if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control)", source);
        Assert.Contains("OnCloneButtonClick();", source);
    }

    [Fact]
    public void Save_ConfirmsMaximumSpendingOverflowBeforeRetry()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Popups",
            "TransactionDetailPopup.xaml.cs"));

        Assert.Contains("result.RequiresConfirmation", source);
        Assert.Contains("MessageBoxButton.YesNo", source);
        Assert.Contains("SaveAsync(allowMaximumSpendingOverflow: true)", source);
    }
}
