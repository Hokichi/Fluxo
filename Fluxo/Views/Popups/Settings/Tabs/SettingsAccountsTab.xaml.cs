using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Fluxo.Core.Enums;
using Fluxo.Resources.CustomControls;
using Fluxo.Resources.Infrastructure;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.Views.Popups.Settings.Tabs;

public partial class SettingsAccountsTab : UserControl
{
    public SettingsAccountsTab()
    {
        InitializeComponent();
    }

    private SettingsAccountsTabVM? _viewModel => DataContext as SettingsAccountsTabVM;

    private async void OnBatchActionClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || !TryParseBatchAction(sender, out var action))
            return;

        if (action == SettingsBatchAction.Delete)
        {
            var selectedItems = _viewModel.Accounts.Where(item => item.IsChecked).ToList();
            if (selectedItems.Count == 1)
            {
                var selectedItem = selectedItems[0];
                var confirmationMessage =
                    await _viewModel.BuildDeleteConfirmationMessageAsync(selectedItem.Id, selectedItem.Name);
                if (FluxoMessageBox.Show(Window.GetWindow(this), confirmationMessage, "Settings", MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    RestoreToggleState(sender);
                    return;
                }
            }
            else if (FluxoMessageBox.Show(Window.GetWindow(this), "Delete the selected items?", "Settings",
                         MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                RestoreToggleState(sender);
                return;
            }
        }

        if (_viewModel.ShouldWarnBeforeApplyingToAll(action) &&
            FluxoMessageBox.Show(Window.GetWindow(this),
                action == SettingsBatchAction.Unpin
                    ? "This will unpin all items in this section. Continue?"
                    : "This will disable all items in this section. Continue?",
                "Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            RestoreToggleState(sender);
            return;
        }

        var result = await _viewModel.ExecuteActionAsync(action);
        if (!result.IsSuccess)
            RestoreToggleState(sender);
        ShowResult(result);
    }

    private void OnChecksToggleClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not BalloonCheckBox checkBox)
            return;

        _viewModel.IsAccountChecksEnabled = checkBox.IsChecked;
        if (!checkBox.IsChecked)
            _viewModel.ClearSelections();
    }

    private void OnSelectionActionClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not BalloonCheckBox checkBox)
            return;

        _viewModel.SetSelections(checkBox.IsChecked);
    }

    private async void OnAddPlaceholderClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        await _viewModel.OpenAddAccountAsync();
    }

    private async void OnRowActionClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null ||
            sender is not FrameworkElement { DataContext: SettingsAccountItemVM sourceItem } ||
            !TryParseBatchAction(sender, out var action))
            return;

        if (action == SettingsBatchAction.Delete)
        {
            var confirmationMessage = await _viewModel.BuildDeleteConfirmationMessageAsync(sourceItem.Id, sourceItem.Name);
            if (FluxoMessageBox.Show(Window.GetWindow(this), confirmationMessage, "Settings", MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                RestoreToggleState(sender);
                return;
            }
        }

        var result = await _viewModel.ExecuteItemActionAsync(sourceItem.Id, action);
        if (!result.IsSuccess)
            RestoreToggleState(sender);
        ShowResult(result);
    }

    private async void OnItemMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var originalSource = e.OriginalSource as DependencyObject;

        if (_viewModel is null ||
            sender is not FrameworkElement { DataContext: SettingsAccountItemVM sourceItem } ||
            ShouldIgnoreRowClick(originalSource))
            return;

        _viewModel.SelectSingleItem(sourceItem.Id);

        if (e.ClickCount < 2 || _viewModel.IsAccountChecksEnabled)
            return;

        await _viewModel.OpenAccountDetailAsync(sourceItem.Id);
    }

    private static bool TryParseBatchAction(object sender, out SettingsBatchAction action)
    {
        action = default;

        if (sender is not FrameworkElement { Tag: string tag })
            return false;

        var parts = tag.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        if (sender is BalloonCheckBox checkBox)
        {
            action = parts[1] switch
            {
                "PinToggle" => checkBox.IsChecked ? SettingsBatchAction.Pin : SettingsBatchAction.Unpin,
                "EnableToggle" => checkBox.IsChecked ? SettingsBatchAction.Enable : SettingsBatchAction.Disable,
                _ => default
            };

            if (parts[1] is "PinToggle" or "EnableToggle")
                return true;
        }

        return Enum.TryParse(parts[1], out action);
    }

    private static bool ShouldIgnoreRowClick(DependencyObject? source)
    {
        var clickedButton = DependencyObjectTree.FindAncestor<ButtonBase>(source);
        return clickedButton is not null && clickedButton is not CheckBox;
    }

    private static void RestoreToggleState(object sender)
    {
        if (sender is BalloonCheckBox checkBox)
            checkBox.IsChecked = !checkBox.IsChecked;
    }

    private void ShowResult(SettingsOperationResult result)
    {
        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorMessage))
            FluxoMessageBox.Show(Window.GetWindow(this), result.ErrorMessage, "Settings", MessageBoxButton.OK,
                MessageBoxImage.Information);
    }
}
