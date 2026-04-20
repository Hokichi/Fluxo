using System.Windows;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Planning;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.Views.CustomControls;
using Fluxo.Views.Popups;
using Fluxo.Views.Popups.Planning;
using Fluxo.Views.Popups.Settings;
using Fluxo.Views.Shell.Wizard;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxo.Services.Dialogs;

public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Func<Window?, string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> _showMessageBox;

    public DialogService(IServiceProvider serviceProvider)
        : this(serviceProvider, FluxoMessageBox.Show)
    {
    }

    public DialogService(IServiceProvider serviceProvider,
        Func<Window?, string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> showMessageBox)
    {
        _serviceProvider = serviceProvider;
        _showMessageBox = showMessageBox;
    }

    public bool? ShowQuickAdd(Window? owner = null)
    {
        return ShowScopedDialog<QuickAddPopup>(owner);
    }

    public bool? ShowQuickSearch(Window? owner = null)
    {
        return ShowScopedDialog<QuickSearchPopup>(owner);
    }

    public bool? ShowSpendingSourcesList(Window? owner = null)
    {
        return ShowScopedDialog<SpendingSourcesListPopup>(owner);
    }

    public bool? ShowSettings(Window? owner = null)
    {
        return ShowScopedDialog<SettingsPopup>(owner);
    }

    public bool? ShowStartupWizard(Window? owner = null)
    {
        return ShowScopedDialog<StartupWizardPopup>(owner);
    }

    public bool? ShowPlanningPopup(Window? owner = null)
    {
        return ShowScopedDialog<PlanningPopup>(owner);
    }

    public bool? ShowPlanningReport(PlanningSnapshot snapshot, Window? owner = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var viewModel = ActivatorUtilities.CreateInstance<PlanningReportVM>(scope.ServiceProvider, snapshot);
        var popup = ActivatorUtilities.CreateInstance<PlanningReportPopup>(scope.ServiceProvider, viewModel);
        return ShowDialog(popup, owner);
    }

    public bool? ShowAddNewTransaction(QuickAddVM viewModel, Window? owner = null)
    {
        return ShowDialog(new AddNewTransaction(viewModel), owner);
    }

    public bool? ShowExpenseDetail(ExpenseDetailVM viewModel, Window? owner = null)
    {
        return ShowDialog(new ExpenseDetailPopup(viewModel), owner);
    }

    public bool? ShowSpendingSourceDetail(SpendingSourceDetailVM viewModel, Window? owner = null)
    {
        return ShowDialog(new SpendingSourceDetailPopup(viewModel, this), owner);
    }

    public bool? ShowTransferFunds(TransferFundsVM viewModel, Window? owner = null)
    {
        return ShowDialog(new TransferFundsPopup(viewModel), owner);
    }

    public bool? ShowAddSpendingSource(Window? owner = null)
    {
        return ShowScopedDialog<AddSpendingSourcePopup>(owner);
    }

    public bool? ShowAddSpendingSource(AddSpendingSourceVM viewModel, Window? owner = null)
    {
        return ShowDialog(new AddSpendingSourcePopup(viewModel), owner);
    }

    public bool? ShowAddFixedExpense(Window? owner = null)
    {
        return ShowScopedDialog<AddFixedExpensePopup>(owner);
    }

    public bool? ShowAddFixedExpense(AddFixedExpenseVM viewModel, Window? owner = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var popup = ActivatorUtilities.CreateInstance<AddFixedExpensePopup>(scope.ServiceProvider, viewModel);
        return ShowDialog(popup, owner);
    }

    public bool? ShowAddSavingGoal(Window? owner = null)
    {
        return ShowScopedDialog<AddSavingGoalPopup>(owner);
    }

    public bool? ShowAddSavingGoal(AddSavingGoalVM viewModel, Window? owner = null)
    {
        return ShowDialog(new AddSavingGoalPopup(viewModel), owner);
    }

    public bool? ShowAddTag(SettingsTagsTabVM settingsViewModel, Window? owner = null)
    {
        return ShowDialog(new AddTagPopup(settingsViewModel, this), owner);
    }

    public (bool? DialogResult, string SelectedHexColor) ShowAddTagColorPicker(string initialHexColor, Window? owner = null)
    {
        var popup = new AddTagColorPickerPopup(initialHexColor);
        var dialogResult = ShowDialog(popup, owner);
        return (dialogResult, popup.SelectedHexColor);
    }

    public bool? ShowFeaturePlaceholder(string title, string message, Window? owner = null)
    {
        return ShowDialog(new FeaturePlaceholderPopup(title, message), owner);
    }

    public (bool? DialogResult, DeleteAllDataChoice Choice) ShowDeleteAllData(Window? owner = null)
    {
        var popup = new DeleteAllDataPopup();
        var dialogResult = ShowDialog(popup, owner);
        return (dialogResult, popup.Choice);
    }

    public MessageBoxResult ShowWarning(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.OK)
    {
        return _showMessageBox(owner, message, title, buttons, MessageBoxImage.Warning);
    }

    public MessageBoxResult ShowError(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.OK)
    {
        return _showMessageBox(owner, message, title, buttons, MessageBoxImage.Error);
    }

    public MessageBoxResult ShowInformation(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.OK)
    {
        return _showMessageBox(owner, message, title, buttons, MessageBoxImage.Information);
    }

    public MessageBoxResult ShowQuestion(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.YesNo)
    {
        return _showMessageBox(owner, message, title, buttons, MessageBoxImage.Question);
    }

    private static bool? ShowDialog(Window popup, Window? owner)
    {
        if (popup.Owner is null)
            popup.Owner = owner;

        return popup.ShowDialog();
    }

    private bool? ShowScopedDialog<TWindow>(Window? owner = null) where TWindow : Window
    {
        using var scope = _serviceProvider.CreateScope();
        var popup = scope.ServiceProvider.GetRequiredService<TWindow>();
        return ShowDialog(popup, owner);
    }
}
