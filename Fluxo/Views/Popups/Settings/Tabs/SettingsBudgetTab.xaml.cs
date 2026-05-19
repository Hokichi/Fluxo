using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.Views.Popups.Settings.Tabs;

public partial class SettingsBudgetTab : UserControl
{
    private readonly DispatcherTimer _allocationHoldDelayTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _allocationRepeatTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private int _heldAllocationDelta;
    private BudgetAllocationSegment _heldAllocationSegment;

    public SettingsBudgetTab()
    {
        InitializeComponent();

        _allocationHoldDelayTimer.Tick += OnAllocationHoldDelayTick;
        _allocationRepeatTimer.Tick += OnAllocationRepeatTick;
        Unloaded += OnUnloaded;
    }

    private SettingsBudgetTabVM? _viewModel => DataContext as SettingsBudgetTabVM;

    private void OnSpendingAmountGateActionClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.OpenAddSpendingSource();
    }

    private void OnAllocationAdjustButtonClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null ||
            sender is not FrameworkElement { Tag: string tag } ||
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

    private void OnUnloaded(object sender, RoutedEventArgs e)
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
        _viewModel?.IncrementAllocation(_heldAllocationSegment, _heldAllocationDelta);
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
}
