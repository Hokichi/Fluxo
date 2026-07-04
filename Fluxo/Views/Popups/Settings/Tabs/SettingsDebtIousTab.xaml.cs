using System.Windows;
using System.Windows.Controls;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.Views.Popups;

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

        int? selectedAccountId = null;
        if (!item.ShouldAffectBalance)
        {
            IReadOnlyList<Fluxo.Core.Entities.Account> accounts;
            try
            {
                accounts = await viewModel.GetResolutionAccountsAsync();
            }
            catch
            {
                MessageBox.Show(Window.GetWindow(this), "Unable to load accounts.", "Debt/IoUs",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (accounts.Count == 0)
            {
                MessageBox.Show(Window.GetWindow(this), "No available accounts.", "Debt/IoUs",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var popup = new IoUAccountSelectionPopup(accounts) { Owner = Window.GetWindow(this) };
            if (popup.ShowDialog() != true)
                return;

            selectedAccountId = popup.SelectedAccountId;
        }

        var result = await viewModel.ResolveAsync(item, selectedAccountId);
        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorMessage))
            MessageBox.Show(Window.GetWindow(this), result.ErrorMessage, "Debt/IoUs",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
