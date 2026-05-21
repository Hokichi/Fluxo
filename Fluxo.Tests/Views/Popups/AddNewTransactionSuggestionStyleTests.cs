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
        Assert.Equal(2, xaml.Split("LostKeyboardFocus=\"OnTransactionNameTextBoxLostKeyboardFocus\"").Length - 1);
        Assert.Contains("OnTransactionNameTextBoxFocusChanged(sender, e);", codeBehind);
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

    [Fact]
    public void NameAndAmountHeaders_ShowRightAlignedValidationHints()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));

        Assert.Contains("x:Key=\"FieldHeaderGridStyle\"", xaml);
        Assert.Equal(2, xaml.Split("Text=\"{Binding NameValidationHint}\"").Length - 1);
        Assert.Equal(3, xaml.Split("Text=\"{Binding AmountValidationHint}\"").Length - 1);
        Assert.Contains("Property=\"HorizontalAlignment\" Value=\"Right\"", xaml);
        Assert.Contains("Property=\"Foreground\" Value=\"{DynamicResource Brush.Danger}\"", xaml);
    }

    [Fact]
    public void NameAndAmountFields_ValidateOnlyOnLostKeyboardFocus()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));
        var codeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml.cs"));

        Assert.Equal(2, xaml.Split("LostKeyboardFocus=\"OnTransactionNameTextBoxLostKeyboardFocus\"").Length - 1);
        Assert.Equal(3, xaml.Split("LostKeyboardFocus=\"OnTransactionAmountTextBoxLostKeyboardFocus\"").Length - 1);
        Assert.Contains("_viewModel.ValidateNameField();", codeBehind);
        Assert.Contains("_viewModel.ValidateAmountField();", codeBehind);
    }
}
