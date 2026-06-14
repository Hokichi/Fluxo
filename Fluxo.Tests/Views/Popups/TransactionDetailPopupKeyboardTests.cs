using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class TransactionDetailPopupKeyboardTests
{
    [Theory]
    [InlineData("ExpenseDetailPopup.xaml.cs")]
    [InlineData("IncomeDetailPopup.xaml.cs")]
    public void CtrlD_ClonesTransactionInDetailPopup(string fileName)
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Popups",
            fileName));

        Assert.Contains("if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control)", source);
        Assert.Contains("OnCloneButtonClick();", source);
    }
}
