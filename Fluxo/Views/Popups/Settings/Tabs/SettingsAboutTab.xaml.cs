using System.Windows;
using System.Windows.Controls;
using Fluxo.Resources.Resources.Messages;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.Views.Popups;

namespace Fluxo.Views.Popups.Settings.Tabs;

public partial class SettingsAboutTab : UserControl
{
    public SettingsAboutTab()
    {
        InitializeComponent();
    }

    private SettingsPersonalizationTabVM? _viewModel => DataContext as SettingsPersonalizationTabVM;

    private async void OnRunSetupWizardClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        if (FluxoMessageBox.Show(Window.GetWindow(this),
                "This will close the current window and open the setup wizard. Continue?",
                "Run Setup Wizard",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _viewModel.RequestClosePopup();
        await ((App)Application.Current).RunSetupWizardAsync();
    }

    private async void OnResetAllSettingsClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        if (FluxoMessageBox.Show(Window.GetWindow(this),
                "Reset all settings to defaults? This keeps your existing data.",
                "Reset Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var result = await _viewModel.ResetAllSettingsAsync();
        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorMessage))
            FluxoMessageBox.Show(Window.GetWindow(this), result.ErrorMessage, "Reset Settings", MessageBoxButton.OK,
                MessageBoxImage.Information);
    }

    private async void OnDeleteAllDataClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        var optionsPopup = new DeleteAllDataPopup { Owner = Window.GetWindow(this) };
        if (optionsPopup.ShowDialog() != true || optionsPopup.Choice == DeleteAllDataChoice.Cancel)
            return;

        var keepSettings = optionsPopup.Choice == DeleteAllDataChoice.KeepSettings;
        var confirmation = FluxoMessageBox.Show(Window.GetWindow(this),
            keepSettings
                ? "This will permanently delete all data and keep your current settings. Continue?"
                : "This will permanently delete all data and settings. Continue?",
            "Delete All Data",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
            return;

        var ownerPopup = Window.GetWindow(this) as SettingsPopup;
        SettingsMaintenanceResult result;
        if (ownerPopup is not null)
        {
            result = SettingsMaintenanceResult.Failure("Unable to delete all data.");
            await ownerPopup.ShowToastWhileAsync("Deleting all data (and settings)", async () =>
            {
                result = await _viewModel.DeleteAllDataAsync(keepSettings);
            });
        }
        else
        {
            result = await _viewModel.DeleteAllDataAsync(keepSettings);
        }

        if (!result.IsSuccess)
        {
            FluxoMessageBox.Show(Window.GetWindow(this), result.ErrorMessage ?? "Unable to delete all data.",
                "Delete All Data", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (FluxoMessageBox.Show(Window.GetWindow(this),
                "All data has been deleted. Would you like to run the setup wizard?",
                "Setup Wizard",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _viewModel.RequestClosePopup();
            await ((App)Application.Current).RunSetupWizardAsync();
        }
    }
}
