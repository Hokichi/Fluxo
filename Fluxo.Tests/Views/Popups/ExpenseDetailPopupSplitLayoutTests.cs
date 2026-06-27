using System;
using System.IO;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class ExpenseDetailPopupSplitLayoutTests
{
    private static readonly string PopupXamlPath = RepositoryPaths.File(
        "Fluxo",
        "Views",
        "Popups",
        "ExpenseDetailPopup.xaml");

    private static readonly string PopupCodeBehindPath = RepositoryPaths.File(
        "Fluxo",
        "Views",
        "Popups",
        "ExpenseDetailPopup.xaml.cs");

    [Fact]
    public void ExpenseDetailPopup_WiresSplitButtonAndHandlers()
    {
        var xaml = File.ReadAllText(PopupXamlPath);
        var codeBehind = File.ReadAllText(PopupCodeBehindPath);

        Assert.Contains("ShowSplitButton=\"{Binding ShowSplitButton}\"", xaml);
        Assert.Contains("protected override async void OnSplitButtonClick()", codeBehind);
        Assert.Contains("await _viewModel.BeginSplitModeAsync();", codeBehind);
        Assert.Contains("SplitExpenseAmountTextBox.Focus();", codeBehind);
        Assert.Contains("ShowSplitButton = _viewModel.ShowSplitButton;", codeBehind);
        Assert.Contains("_viewModel.CanCloseSplitModeWithoutSaving", codeBehind);
        Assert.Contains("_viewModel.RequiresEmptySplitConfirmationOnClose", codeBehind);
    }

    [Fact]
    public void ExpenseDetailPopup_SplitSaveReturnsToViewModeWithoutClosingPopup()
    {
        var codeBehind = File.ReadAllText(PopupCodeBehindPath);

        Assert.DoesNotContain("if (wasSplitModeSave)", codeBehind);
        Assert.DoesNotContain("_allowClose = true;\r\n            Close();", codeBehind);
        Assert.Contains("SyncNoteDocumentFromViewModel();", codeBehind);
    }

    [Fact]
    public void ExpenseDetailPopup_SplitModeLayout_HidesNormalFieldsAndShowsSplitRows()
    {
        var xaml = File.ReadAllText(PopupXamlPath);

        Assert.Contains("x:Name=\"NormalExpenseNameDatePanel\"", xaml);
        Assert.Contains("x:Name=\"NormalExpenseCategorySourcePanel\"", xaml);
        Assert.Contains("x:Name=\"NormalTagsPanel\"", xaml);
        Assert.Contains("x:Name=\"NormalExpenseNotePanel\"", xaml);
        Assert.Contains("ShowNormalExpenseFields", xaml);
        Assert.Contains("x:Name=\"SplitRowsPanel\"", xaml);
        Assert.Contains("x:Name=\"SplitRowsItemsControl\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding SplitRows}\"", xaml);
        Assert.Contains("Click=\"OnAddSplitRowClick\"", xaml);
        Assert.Contains("Click=\"OnRemoveSplitRowClick\"", xaml);
        Assert.Contains("Split amounts exceed the original expense amount.", xaml);
        Assert.Contains("x:Key=\"SplitRowTagToggleStyle\"", xaml);
        Assert.Contains("x:Name=\"SplitRowsHeaderGrid\"", xaml);
        Assert.Contains("Text=\"Set as lend\"", xaml);
        Assert.Contains("IsChecked=\"{Binding IsLend, Mode=TwoWay}\"", xaml);

        var addExpenseButtonIndex = xaml.IndexOf("Content=\"Add Sub-expense\"", StringComparison.Ordinal);
        var splitRowsHeaderIndex = xaml.IndexOf("x:Name=\"SplitRowsHeaderGrid\"", StringComparison.Ordinal);
        var splitRowsItemsIndex = xaml.IndexOf("x:Name=\"SplitRowsItemsControl\"", StringComparison.Ordinal);
        Assert.True(addExpenseButtonIndex >= 0);
        Assert.True(splitRowsHeaderIndex >= 0);
        Assert.True(splitRowsItemsIndex >= 0);
        Assert.True(addExpenseButtonIndex < splitRowsHeaderIndex);
        Assert.True(addExpenseButtonIndex < splitRowsItemsIndex);
    }

    [Fact]
    public void ExpenseDetailPopup_NormalLayout_PutsNameAndPinAboveAmount()
    {
        var xaml = File.ReadAllText(PopupXamlPath);
        var codeBehind = File.ReadAllText(PopupCodeBehindPath);

        var namePanelIndex = xaml.IndexOf("x:Name=\"NormalExpenseNamePinPanel\"", StringComparison.Ordinal);
        var pinIndex = xaml.IndexOf("x:Name=\"ExpensePinButton\"", StringComparison.Ordinal);
        var amountIndex = xaml.IndexOf("x:Name=\"ExpenseAmountTextBox\"", StringComparison.Ordinal);

        Assert.True(namePanelIndex >= 0);
        Assert.True(pinIndex > namePanelIndex);
        Assert.True(amountIndex > pinIndex);
        Assert.Contains("UncheckedIcon=\"{StaticResource Pin}\"", xaml);
        Assert.Contains("IsChecked=\"{Binding IsPinned, Mode=TwoWay}\"", xaml);
        Assert.Contains("ShowDeleteButton=\"True\"", xaml);
        Assert.Contains("protected override async void OnDeleteButtonClick()", codeBehind);
    }

    [Fact]
    public void ExpenseDetailPopup_SplitRowTagPill_UsesEllipsisAndPopupSelection()
    {
        var xaml = File.ReadAllText(PopupXamlPath);

        Assert.Contains("IsTagPopupOpen", xaml);
        Assert.Contains("TagDisplayName", xaml);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml);
        Assert.Contains("AllSplitTags", xaml);
        Assert.Contains("SelectedItem=\"{Binding SelectedTag, Mode=TwoWay}\"", xaml);
        Assert.Contains("x:Name=\"SplitRowTagToggleButton\"", xaml);
        Assert.Contains("Tag=\"{Binding SelectedTag.HexCode}\"", xaml);
        Assert.Contains("PlacementTarget=\"{Binding ElementName=SplitRowTagToggleButton}\"", xaml);
    }

    [Fact]
    public void ExpenseDetailPopup_ChildTransactionsPanel_IsRightSideScrollableList()
    {
        var xaml = File.ReadAllText(PopupXamlPath);
        var codeBehind = File.ReadAllText(PopupCodeBehindPath);

        Assert.Contains("Width=\"{Binding DetailPopupWidth}\"", xaml);
        Assert.Contains("x:Key=\"ChildTransactionItemTemplate\"", xaml);
        Assert.Contains("Grid.Column=\"2\"", xaml);
        Assert.Contains("Text=\"Sub-transactions\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding ChildTransactions}\"", xaml);
        Assert.Contains("Data=\"{StaticResource CreditCardMinusSolid}\"", xaml);
        Assert.Contains("Visibility=\"{Binding IsLend, Converter={StaticResource BoolToVisibilityConverter}}\"", xaml);
        Assert.Contains("Visibility=\"{Binding ShowChildTransactions, Converter={StaticResource BoolToVisibilityConverter}}\"", xaml);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", xaml);
        Assert.Contains("await _viewModel.LoadChildTransactionsAsync();", codeBehind);
    }
}
