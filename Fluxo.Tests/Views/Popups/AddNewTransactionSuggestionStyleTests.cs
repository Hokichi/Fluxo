using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class AddNewTransactionSuggestionStyleTests
{
    [Fact]
    public void TransactionNameSuggestions_UseHoverBackgroundResource()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));

        Assert.Contains("x:Key=\"TransactionNameSuggestionListBoxItemStyle\"", xaml);
        Assert.Contains("TargetName=\"ItemBackground\" Property=\"Background\" Value=\"{DynamicResource Brush.Background.Hover}\"", xaml);
        Assert.Equal(2, xaml.Split("ItemContainerStyle=\"{StaticResource TransactionNameSuggestionListBoxItemStyle}\"").Length - 1);
    }

    [Fact]
    public void TransactionNameTextBoxes_SyncSuggestionsFromKeyboardFocusChanges()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));
        var codeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml.cs"));

        Assert.Equal(2, xaml.Split("GotKeyboardFocus=\"OnTransactionNameTextBoxFocusChanged\"").Length - 1);
        Assert.Equal(2, xaml.Split("LostKeyboardFocus=\"OnTransactionNameTextBoxFocusChanged\"").Length - 1);
        Assert.DoesNotContain("SyncNameSuggestionsPopupState();\r\n\r\n        if (!_viewModel.IsMoreTagsOpen", codeBehind);
    }

    [Fact]
    public void PopupTags_AreRadioButtonsToPreventClearingTheRequiredSelection()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));
        var codeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml.cs"));

        Assert.Contains("<RadioButton", xaml);
        Assert.Contains("Style=\"{StaticResource PopupTagItemRadioStyle}\"", xaml);
        Assert.DoesNotContain("Style=\"{StaticResource PopupTagItemToggleStyle}\" />", xaml);
        Assert.Contains("new RadioButton", codeBehind);
        Assert.Contains("PopupTagItemRadioStyle", codeBehind);
    }
}
