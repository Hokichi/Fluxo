using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.Views.Popups.Settings;

namespace Fluxo.Views.Popups.Settings.Tabs;

public partial class SettingsFixedExpensesTab : UserControl
{
    public SettingsFixedExpensesTab()
    {
        InitializeComponent();
    }

    private static SettingsPopup? FindPopup(DependencyObject source) => Window.GetWindow(source) as SettingsPopup;

    private void OnBatchActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnBatchActionClick(sender, e);
    }

    private void OnChecksToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnChecksToggleClick(sender, e);
    }

    private void OnSelectionActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnSelectionActionClick(sender, e);
    }

    private void OnAddPlaceholderClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnAddPlaceholderClick(sender, e);
    }

    private void OnRowActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnRowActionClick(sender, e);
    }

    private void OnItemMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnFixedExpenseItemMouseLeftButtonDown(sender, e);
    }
}
