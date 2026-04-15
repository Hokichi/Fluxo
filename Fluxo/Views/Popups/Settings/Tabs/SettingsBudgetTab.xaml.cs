using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.Views.Popups.Settings;

namespace Fluxo.Views.Popups.Settings.Tabs;

public partial class SettingsBudgetTab : UserControl
{
    public SettingsBudgetTab()
    {
        InitializeComponent();
    }

    private static SettingsPopup? FindPopup(DependencyObject source) => Window.GetWindow(source) as SettingsPopup;

    private void OnAllocationAdjustButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnAllocationAdjustButtonClick(sender, e);
    }

    private void OnAllocationAdjustButtonMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnAllocationAdjustButtonMouseDown(sender, e);
    }

    private void OnAllocationAdjustButtonMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnAllocationAdjustButtonMouseUp(sender, e);
    }

    private void OnAllocationAdjustButtonMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnAllocationAdjustButtonMouseLeave(sender, e);
    }
}
