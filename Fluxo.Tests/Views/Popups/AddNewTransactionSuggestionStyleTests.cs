using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class AddNewTransactionSuggestionStyleTests
{
    [Fact]
    public void TransactionTypeSelector_UsesSharedSegmentedToggleGroup()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));

        Assert.Contains("<customControls:SegmentedToggleGroup", xaml);
        Assert.Equal(3, xaml.Split("<customControls:SegmentedToggleOption", StringSplitOptions.None).Length - 1);
        Assert.Contains("Content=\"Expense\"", xaml);
        Assert.Contains("IsSelected=\"{Binding IsExpense, Mode=TwoWay}\"", xaml);
        Assert.Contains("Content=\"Income\"", xaml);
        Assert.Contains("IsSelected=\"{Binding IsIncome, Mode=TwoWay}\"", xaml);
        Assert.Contains("Content=\"Goal Update\"", xaml);
        Assert.Contains("IsSelected=\"{Binding IsGoal, Mode=TwoWay}\"", xaml);
        Assert.DoesNotContain("GroupName=\"TransactionType\"", xaml);
    }

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
    public void ExpenseCategoryComboBox_DimsDisabledCategoryItems()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));

        Assert.Contains("x:Key=\"ExpenseCategoryComboBoxItemStyle\"", xaml);
        Assert.Contains("BasedOn=\"{StaticResource FluxoComboBoxItemStyle}\"", xaml);
        Assert.Contains("Binding=\"{Binding IsEnabled}\" Value=\"False\"", xaml);
        Assert.Contains("Property=\"Opacity\" Value=\"0.45\"", xaml);
        Assert.Contains("ItemContainerStyle=\"{StaticResource ExpenseCategoryComboBoxItemStyle}\"", xaml);
    }

    [Fact]
    public void NameFieldsValidateOnLostFocus_AmountFieldsValidateOnTextChangedAndLostFocus()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));
        var codeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml.cs"));

        Assert.Equal(2, xaml.Split("LostKeyboardFocus=\"OnTransactionNameTextBoxLostKeyboardFocus\"").Length - 1);
        Assert.Equal(3, xaml.Split("LostKeyboardFocus=\"OnTransactionAmountTextBoxLostKeyboardFocus\"").Length - 1);
        Assert.Equal(3, xaml.Split("TextChanged=\"OnTransactionAmountTextBoxTextChanged\"").Length - 1);
        Assert.Contains("_viewModel.ValidateNameField();", codeBehind);
        Assert.Contains("_viewModel.ValidateAmountField();", codeBehind);
        Assert.Contains("OnTransactionAmountTextBoxTextChanged", codeBehind);
        Assert.Contains("GetBindingExpression(TextBox.TextProperty)?.UpdateSource()", codeBehind);
        Assert.Contains("_viewModel.ActivateAmountValidation();", codeBehind);
        Assert.Contains("IsKeyboardFocusWithin: true", codeBehind);

        var updateSourceIndex = codeBehind.IndexOf(
            "GetBindingExpression(TextBox.TextProperty)?.UpdateSource()",
            StringComparison.Ordinal);
        var activateValidationIndex = codeBehind.IndexOf(
            "_viewModel.ActivateAmountValidation();",
            StringComparison.Ordinal);
        Assert.True(updateSourceIndex >= 0);
        Assert.True(activateValidationIndex >= 0);
        Assert.True(updateSourceIndex < activateValidationIndex);
    }
}
