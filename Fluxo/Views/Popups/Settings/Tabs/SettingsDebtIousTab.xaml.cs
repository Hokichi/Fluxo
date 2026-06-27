using System.Windows;
using System.Windows.Controls;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.Views.Popups.Settings.Tabs;

public partial class SettingsIoUsTab : UserControl
{
    public SettingsIoUsTab()
    {
        InitializeComponent();
    }

    private async void OnResolveClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsIoUsTabVM viewModel ||
            sender is not FrameworkElement { DataContext: IoUItemVM item })
        {
            return;
        }

        var result = await viewModel.ResolveAsync(item);
        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorMessage))
            MessageBox.Show(Window.GetWindow(this), result.ErrorMessage, "Debt/IoUs",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
