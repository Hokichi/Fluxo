using System.Windows;
using Fluxo.Resources.CustomControls;
using Fluxo.Services.Updates;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.Views.Popups.Settings;

public static class SettingsUpdateCheckFlow
{
    public static async Task CheckForUpdatesAsync(Window? owner, SettingsPersonalizationTabVM? viewModel)
    {
        if (viewModel is null)
            return;

        var ownerPopup = owner as SettingsPopup;
        var update = await CheckForUpdatesWithOptionalToastAsync(ownerPopup, viewModel);

        switch (update.Status)
        {
            case AppUpdateCheckStatus.UpToDate:
                if (ownerPopup is null)
                {
                    FluxoMessageBox.Show(
                        owner,
                        "fluxo is up to date.",
                        "Check for Updates",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                return;

            case AppUpdateCheckStatus.Error:
                FluxoMessageBox.Show(
                    owner,
                    update.ErrorMessage ?? "Unable to check for updates.",
                    "Check for Updates",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;

            case AppUpdateCheckStatus.UpdateAvailable:
                await viewModel.HandleAvailableUpdateAsync(update, owner);
                return;
        }
    }

    private static async Task<AppUpdateCheckResult> CheckForUpdatesWithOptionalToastAsync(
        SettingsPopup? ownerPopup,
        SettingsPersonalizationTabVM viewModel)
    {
        if (ownerPopup is null)
            return await viewModel.CheckForUpdatesAsync();

        return await ownerPopup.ShowToastWhileAsync("Checking for updates", async toast =>
        {
            var update = await viewModel.CheckForUpdatesAsync();
            if (update.Status == AppUpdateCheckStatus.UpToDate)
            {
                await toast.UpdateMessageAsync("fluxo is up to date.");
                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            return update;
        });
    }
}
