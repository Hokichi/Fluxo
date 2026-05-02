using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.Views.Popups.Settings.Tabs;

public partial class SettingsSourcesTab : UserControl
{
    public SettingsSourcesTab()
    {
        InitializeComponent();
    }

    private SettingsSourcesTabVM? _viewModel => DataContext as SettingsSourcesTabVM;

    private async void OnBatchActionClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || !TryParseBatchAction(sender, out var action))
            return;

        if (action == SettingsBatchAction.Delete &&
            FluxoMessageBox.Show(Window.GetWindow(this), "Delete the selected items?", "Settings", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        if (_viewModel.ShouldWarnBeforeApplyingToAll(action) &&
            FluxoMessageBox.Show(Window.GetWindow(this),
                action == SettingsBatchAction.Hide
                    ? "This will hide all items in this section. Continue?"
                    : "This will disable all items in this section. Continue?",
                "Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var result = await _viewModel.ExecuteActionAsync(action);
        ShowResult(result);
    }

    private void OnChecksToggleClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || !TryParseChecksToggle(sender, out var isEnabled))
            return;

        _viewModel.IsSpendingSourceChecksEnabled = isEnabled;
        if (!isEnabled)
            _viewModel.ClearSelections();
    }

    private void OnSelectionActionClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || !TryParseSelectionAction(sender, out var shouldCheck))
            return;

        _viewModel.SetSelections(shouldCheck);
    }

    private async void OnAddPlaceholderClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        await _viewModel.OpenAddSpendingSourceAsync();
    }

    private async void OnRowActionClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null ||
            sender is not FrameworkElement { DataContext: SettingsSpendingSourceItemVM sourceItem } ||
            !TryParseBatchAction(sender, out var action))
            return;

        if (action == SettingsBatchAction.Delete &&
            FluxoMessageBox.Show(Window.GetWindow(this), $"Delete \"{sourceItem.Name}\"?", "Settings", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var result = await _viewModel.ExecuteItemActionAsync(sourceItem.Id, action);
        ShowResult(result);
    }

    private async void OnItemMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var originalSource = e.OriginalSource as DependencyObject;

        if (_viewModel is null ||
            sender is not FrameworkElement { DataContext: SettingsSpendingSourceItemVM sourceItem } ||
            ShouldIgnoreRowClick(originalSource))
            return;

        _viewModel.SelectSingleItem(sourceItem.Id);

        if (e.ClickCount < 2 || IsCheckBoxClick(originalSource))
            return;

        await _viewModel.OpenSpendingSourceDetailAsync(sourceItem.Id);
    }

    private static bool TryParseBatchAction(object sender, out SettingsBatchAction action)
    {
        action = default;

        if (sender is not FrameworkElement { Tag: string tag })
            return false;

        var parts = tag.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 && Enum.TryParse(parts[1], out action);
    }

    private static bool TryParseChecksToggle(object sender, out bool isEnabled)
    {
        isEnabled = false;

        if (sender is not FrameworkElement { Tag: string tag })
            return false;

        var parts = tag.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        isEnabled = string.Equals(parts[1], "EnableChecks", StringComparison.Ordinal);
        return true;
    }

    private static bool TryParseSelectionAction(object sender, out bool shouldCheck)
    {
        shouldCheck = false;

        if (sender is not FrameworkElement { Tag: string tag })
            return false;

        var parts = tag.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        shouldCheck = string.Equals(parts[1], "CheckAll", StringComparison.Ordinal);
        return true;
    }

    private static bool ShouldIgnoreRowClick(DependencyObject? source)
    {
        var clickedButton = FindAncestor<ButtonBase>(source);
        return clickedButton is not null && clickedButton is not CheckBox;
    }

    private static bool IsCheckBoxClick(DependencyObject? source)
    {
        return FindAncestor<CheckBox>(source) is not null;
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T typedSource)
                return typedSource;

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void ShowResult(SettingsOperationResult result)
    {
        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorMessage))
            FluxoMessageBox.Show(Window.GetWindow(this), result.ErrorMessage, "Settings", MessageBoxButton.OK,
                MessageBoxImage.Information);
    }
}
