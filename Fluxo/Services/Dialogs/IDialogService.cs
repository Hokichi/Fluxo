using System;
using System.Threading.Tasks;
using System.Windows;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Planning;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.Services.Updates;
using Fluxo.Views.Popups;

namespace Fluxo.Services.Dialogs;

public interface IDialogService
{
    bool? ShowQuickAdd(Window? owner = null);

    bool? ShowSpendingSourcesList(Window? owner = null);

    bool? ShowSettings(Window? owner = null);

    bool? ShowQuickSetupWizard(Window? owner = null);

    bool? ShowPlanningPopup(Window? owner = null);

    bool? ShowPlanningReport(PlanningSnapshot snapshot, Window? owner = null);

    bool? ShowAddNewTransaction(QuickAddVM viewModel, Window? owner = null);

    bool? ShowExpenseDetail(ExpenseDetailVM viewModel, Window? owner = null);

    bool? ShowSpendingSourceDetail(SpendingSourceDetailVM viewModel, Window? owner = null);

    bool? ShowTransferFunds(TransferFundsVM viewModel, Window? owner = null);

    bool? ShowAddSpendingSource(Window? owner = null);

    bool? ShowAddSpendingSource(AddSpendingSourceVM viewModel, Window? owner = null);

    bool? ShowAddSavingGoal(Window? owner = null);

    bool? ShowAddSavingGoal(AddSavingGoalVM viewModel, Window? owner = null);

    bool? ShowNotificationChecklistAction(NotificationChecklistActionVM viewModel, Window? owner = null);

    bool? ShowGoalDeadlineAction(GoalDeadlineActionVM viewModel, Window? owner = null);

    bool? ShowAddTag(SettingsTagsTabVM settingsViewModel, Window? owner = null);
    bool? ShowAddTag(AddTagVM viewModel, Func<string, string, Task<SettingsOperationResult>> saveTagAsync,
        Window? owner = null);
    bool? ShowAddTag(Func<string, string, Task<SettingsOperationResult>> createTagAsync, Window? owner = null);

    (bool? DialogResult, string SelectedHexColor) ShowAddTagColorPicker(string initialHexColor, Window? owner = null);

    bool? ShowFeaturePlaceholder(string title, string message, Window? owner = null);

    (bool? DialogResult, DeleteAllDataChoice Choice) ShowDeleteAllData(Window? owner = null);

    Task ShowToastWhileAsync(string message, Func<Task> work, Window? owner = null);

    Task ShowToastWhileAsync(string message, Action work, Window? owner = null);

    Task<string?> ShowDownloadUpdateAsync(
        AppUpdateCheckResult update,
        Func<IProgress<double>, CancellationToken, Task<string>> downloadInstallerAsync,
        Window? owner = null);

    MessageBoxResult ShowWarning(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.OK);

    MessageBoxResult ShowError(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.OK);

    MessageBoxResult ShowInformation(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.OK);

    MessageBoxResult ShowQuestion(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.YesNo);
}
