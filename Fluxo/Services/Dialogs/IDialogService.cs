using System.Windows;
using Fluxo.ViewModels.Popups;
using Fluxo.Views.Popups;

namespace Fluxo.Services.Dialogs;

public interface IDialogService
{
    bool? ShowQuickAdd(Window? owner = null);

    bool? ShowQuickSearch(Window? owner = null);

    bool? ShowSpendingSourcesList(Window? owner = null);

    bool? ShowSettings(Window? owner = null);

    bool? ShowStartupWizard(Window? owner = null);

    bool? ShowAddNewTransaction(QuickAddVM viewModel, Window? owner = null);

    bool? ShowExpenseDetail(ExpenseDetailVM viewModel, Window? owner = null);

    bool? ShowSpendingSourceDetail(SpendingSourceDetailVM viewModel, Window? owner = null);

    bool? ShowTransferFunds(TransferFundsVM viewModel, Window? owner = null);

    bool? ShowAddSpendingSource(Window? owner = null);

    bool? ShowAddSpendingSource(AddSpendingSourceVM viewModel, Window? owner = null);

    bool? ShowAddFixedExpense(Window? owner = null);

    bool? ShowAddFixedExpense(AddFixedExpenseVM viewModel, Window? owner = null);

    bool? ShowAddSavingGoal(Window? owner = null);

    bool? ShowAddSavingGoal(AddSavingGoalVM viewModel, Window? owner = null);

    bool? ShowAddTag(SettingsVM settingsViewModel, Window? owner = null);

    (bool? DialogResult, string SelectedHexColor) ShowAddTagColorPicker(string initialHexColor, Window? owner = null);

    bool? ShowFeaturePlaceholder(string title, string message, Window? owner = null);

    (bool? DialogResult, DeleteAllDataChoice Choice) ShowDeleteAllData(Window? owner = null);

    MessageBoxResult ShowWarning(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.OK);

    MessageBoxResult ShowError(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.OK);

    MessageBoxResult ShowInformation(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.OK);

    MessageBoxResult ShowQuestion(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.YesNo);
}
