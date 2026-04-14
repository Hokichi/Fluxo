using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Fluxo.Resources.CustomControls;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;

namespace Fluxo.Views.Popups.Settings;

public partial class SettingsPopup : BasePopup
{
    private readonly DispatcherTimer _allocationHoldDelayTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _allocationRepeatTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly SettingsVM _viewModel;
    private bool _allowClose;
    private int _heldAllocationDelta;
    private BudgetAllocationSegment _heldAllocationSegment;
    private bool _isHandlingCloseRequest;

    public SettingsPopup(SettingsVM viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoadedAsync;
        Closing += OnPopupClosing;
        _allocationHoldDelayTimer.Tick += OnAllocationHoldDelayTick;
        _allocationRepeatTimer.Tick += OnAllocationRepeatTick;
    }

    protected override async void OnSaveButtonClick()
    {
        var result = await _viewModel.ApplyConfigurationAsync();
        if (!result.IsSuccess)
            ShowMessage(result.ErrorMessage, "Settings");
    }

    protected override void OnRevertButtonClick()
    {
        _viewModel.RevertConfigurationChanges();
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.LoadAsync();
        }
        catch (Exception exception)
        {
            ShowMessage($"Unable to load settings.\n\n{exception.Message}", "Settings");
            _allowClose = true;
            Close();
        }
    }

    private async void OnPopupClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || _isHandlingCloseRequest || !_viewModel.HasPendingConfigurationChanges)
            return;

        e.Cancel = true;
        _isHandlingCloseRequest = true;

        try
        {
            if (FluxoMessageBox.Show(this, "Apply pending settings before closing?", "Settings",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var result = await _viewModel.ApplyConfigurationAsync();
                if (!result.IsSuccess)
                {
                    ShowMessage(result.ErrorMessage, "Settings");
                    return;
                }
            }

            _allowClose = true;
        }
        finally
        {
            _isHandlingCloseRequest = false;
        }
    }

    private async void OnBatchActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag })
            return;

        var parts = tag.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Enum.TryParse<SettingsBatchAction>(parts[1], out var action))
            return;

        if (!TryParseBatchTarget(parts[0], out var target))
            return;

        if (action == SettingsBatchAction.Delete &&
            FluxoMessageBox.Show(this, "Delete the selected items?", "Settings", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        if (_viewModel.ShouldWarnBeforeApplyingToAll(target, action) &&
            FluxoMessageBox.Show(this,
                action == SettingsBatchAction.Hide
                    ? "This will hide all items in this section. Continue?"
                    : "This will disable all items in this section. Continue?",
                "Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var result = target switch
        {
            SettingsBatchTarget.SpendingSources => await _viewModel.ExecuteSpendingSourceActionAsync(action),
            SettingsBatchTarget.FixedExpenses => await _viewModel.ExecuteFixedExpenseActionAsync(action),
            SettingsBatchTarget.Goals => await _viewModel.ExecuteGoalActionAsync(action),
            _ => SettingsOperationResult.Failure("Unsupported settings action.")
        };

        if (!result.IsSuccess)
            ShowMessage(result.ErrorMessage, "Settings");
    }

    private void OnChecksToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag })
            return;

        var parts = tag.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return;

        var isEnabled = string.Equals(parts[1], "EnableChecks", StringComparison.Ordinal);

        switch (parts[0])
        {
            case "SpendingSources":
                _viewModel.IsSpendingSourceChecksEnabled = isEnabled;
                if (!isEnabled)
                    _viewModel.ClearSelections(SettingsBatchTarget.SpendingSources);
                break;

            case "FixedExpenses":
                _viewModel.IsFixedExpenseChecksEnabled = isEnabled;
                if (!isEnabled)
                    _viewModel.ClearSelections(SettingsBatchTarget.FixedExpenses);
                break;

            case "Goals":
                _viewModel.IsGoalChecksEnabled = isEnabled;
                if (!isEnabled)
                    _viewModel.ClearSelections(SettingsBatchTarget.Goals);
                break;
        }
    }

    private void OnSelectionActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag })
            return;

        var parts = tag.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !TryParseBatchTarget(parts[0], out var target))
            return;

        var shouldCheck = string.Equals(parts[1], "CheckAll", StringComparison.Ordinal);
        _viewModel.SetSelections(target, shouldCheck);
    }

    private void OnAddPlaceholderClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string title })
            return;

        if (string.Equals(title, "Add New Spending Source", StringComparison.Ordinal))
        {
            new AddSpendingSourcePopup(_viewModel.CreateAddSpendingSourceViewModel()) { Owner = this }.ShowDialog();
            return;
        }

        new FeaturePlaceholderPopup(title, "This creation flow is still being built.") { Owner = this }.ShowDialog();
    }

    private async void OnRowActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag })
            return;

        var parts = tag.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Enum.TryParse<SettingsBatchAction>(parts[1], out var action))
            return;

        var itemLabel = sender switch
        {
            FrameworkElement { DataContext: SettingsSpendingSourceItemVM sourceItem } => sourceItem.Name,
            FrameworkElement { DataContext: SettingsFixedExpenseItemVM fixedExpenseItem } => fixedExpenseItem.Name,
            FrameworkElement { DataContext: SettingsSavingGoalItemVM goalItem } => goalItem.Name,
            _ => "this item"
        };

        if (action == SettingsBatchAction.Delete &&
            FluxoMessageBox.Show(this, $"Delete \"{itemLabel}\"?", "Settings", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var result = parts[0] switch
        {
            "SpendingSources" when sender is FrameworkElement
            {
                DataContext: SettingsSpendingSourceItemVM sourceItem
            } =>
                await _viewModel.ExecuteSpendingSourceItemActionAsync(sourceItem.Id, action),
            "FixedExpenses" when sender is FrameworkElement
            {
                DataContext: SettingsFixedExpenseItemVM fixedExpenseItem
            } =>
                await _viewModel.ExecuteFixedExpenseItemActionAsync(fixedExpenseItem.Id, action),
            "Goals" when sender is FrameworkElement { DataContext: SettingsSavingGoalItemVM goalItem } =>
                await _viewModel.ExecuteGoalItemActionAsync(goalItem.Id, action),
            _ => SettingsOperationResult.Failure("Unsupported settings action.")
        };

        if (!result.IsSuccess)
            ShowMessage(result.ErrorMessage, "Settings");
    }

    private void OnAddTagClick(object sender, RoutedEventArgs e)
    {
        new AddTagPopup(_viewModel) { Owner = this }.ShowDialog();
    }

    private async void OnRunSetupWizardClick(object sender, RoutedEventArgs e)
    {
        if (FluxoMessageBox.Show(this,
                "This will close the current window and open the setup wizard. Continue?",
                "Run Setup Wizard",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _allowClose = true;
        Close();
        await ((App)Application.Current).RunSetupWizardAsync();
    }

    private async void OnResetAllSettingsClick(object sender, RoutedEventArgs e)
    {
        if (FluxoMessageBox.Show(this,
                "Reset all settings to defaults? This keeps your existing data.",
                "Reset Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var result = await _viewModel.ResetAllSettingsAsync();
        if (!result.IsSuccess)
            ShowMessage(result.ErrorMessage, "Reset Settings");
    }

    private async void OnDeleteAllDataClick(object sender, RoutedEventArgs e)
    {
        var optionsPopup = new DeleteAllDataPopup { Owner = this };
        if (optionsPopup.ShowDialog() != true || optionsPopup.Choice == DeleteAllDataChoice.Cancel)
            return;

        var keepSettings = optionsPopup.Choice == DeleteAllDataChoice.KeepSettings;
        var confirmation = FluxoMessageBox.Show(this,
            keepSettings
                ? "This will permanently delete all data and keep your current settings. Continue?"
                : "This will permanently delete all data and settings. Continue?",
            "Delete All Data",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
            return;

        var result = await _viewModel.DeleteAllDataAsync(keepSettings);
        if (!result.IsSuccess)
        {
            ShowMessage(result.ErrorMessage, "Delete All Data");
            return;
        }

        if (FluxoMessageBox.Show(this,
                "All data has been deleted. Would you like to run the setup wizard?",
                "Setup Wizard",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _allowClose = true;
            Close();
            await ((App)Application.Current).RunSetupWizardAsync();
        }
    }

    private async void OnTagDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ExpenseTagVM tag })
            return;

        if (FluxoMessageBox.Show(this, $"Delete the tag \"{tag.Name}\"?", "Tags", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var result = await _viewModel.DeleteTagAsync(tag);
        if (!result.IsSuccess)
            ShowMessage(result.ErrorMessage, "Tags");
    }

    private void OnAllocationAdjustButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag } ||
            !TryParseAllocationTag(tag, out var segment, out var delta))
            return;

        _viewModel.IncrementAllocation(segment, delta);
    }

    private void OnAllocationAdjustButtonMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag } ||
            !TryParseAllocationTag(tag, out var segment, out var delta))
            return;

        _heldAllocationSegment = segment;
        _heldAllocationDelta = delta;
        _allocationRepeatTimer.Stop();
        _allocationHoldDelayTimer.Stop();
        _allocationHoldDelayTimer.Start();
    }

    private void OnAllocationAdjustButtonMouseUp(object sender, MouseButtonEventArgs e)
    {
        StopAllocationTimers();
    }

    private void OnAllocationAdjustButtonMouseLeave(object sender, MouseEventArgs e)
    {
        StopAllocationTimers();
    }

    private void OnAllocationHoldDelayTick(object? sender, EventArgs e)
    {
        _allocationHoldDelayTimer.Stop();
        _allocationRepeatTimer.Start();
    }

    private void OnAllocationRepeatTick(object? sender, EventArgs e)
    {
        _viewModel.IncrementAllocation(_heldAllocationSegment, _heldAllocationDelta);
    }

    private void StopAllocationTimers()
    {
        _allocationHoldDelayTimer.Stop();
        _allocationRepeatTimer.Stop();
    }

    private static bool TryParseAllocationTag(string tag, out BudgetAllocationSegment segment, out int delta)
    {
        segment = default;
        delta = 0;

        var parts = tag.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Enum.TryParse(parts[0], out segment))
            return false;

        delta = string.Equals(parts[1], "+1", StringComparison.Ordinal) ? 1 : -1;
        return true;
    }

    private static bool TryParseBatchTarget(string value, out SettingsBatchTarget target)
    {
        target = value switch
        {
            "SpendingSources" => SettingsBatchTarget.SpendingSources,
            "FixedExpenses" => SettingsBatchTarget.FixedExpenses,
            "Goals" => SettingsBatchTarget.Goals,
            _ => default
        };

        return value is "SpendingSources" or "FixedExpenses" or "Goals";
    }

    private void ShowMessage(string? message, string title)
    {
        if (!string.IsNullOrWhiteSpace(message))
            FluxoMessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}