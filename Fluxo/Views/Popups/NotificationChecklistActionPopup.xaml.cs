using System.Windows;
using System.Windows.Controls;
using System.Runtime.ExceptionServices;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.Services.Dialogs;

namespace Fluxo.Views.Popups;

public partial class NotificationChecklistActionPopup : BasePopup
{
    private readonly IDialogService _dialogService;
    private readonly SettingsTagsTabVM _settingsTagsTabViewModel;
    private readonly NotificationChecklistActionVM _viewModel;
    private bool _isProcessing;

    public NotificationChecklistActionPopup(
        NotificationChecklistActionVM viewModel,
        IDialogService dialogService,
        SettingsTagsTabVM settingsTagsTabViewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _dialogService = dialogService;
        _settingsTagsTabViewModel = settingsTagsTabViewModel;
        DataContext = _viewModel;
    }

    private async void OnAddTagClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { TemplatedParent: ComboBox comboBox })
            comboBox.IsDropDownOpen = false;

        _dialogService.ShowAddTag(_settingsTagsTabViewModel, this);
        await _viewModel.RefreshAvailableTagsAsync();
    }

    private void OnProceedClick(object sender, RoutedEventArgs e)
    {
        if (_isProcessing || !_viewModel.CanProceed)
            return;

        _isProcessing = true;

        try
        {
            var processed = false;
            ToastPopup? toast = null;
            toast = new ToastPopup("Processing...", async () =>
            {
                processed = await _viewModel.ProcessAsync();
                if (!processed)
                    return;

                await toast!.UpdateMessageAsync("Processed");
                await Task.Delay(TimeSpan.FromSeconds(2));
            })
            {
                Owner = this
            };

            toast.ShowDialog();

            if (toast.ExecutionException is not null)
                ExceptionDispatchInfo.Capture(toast.ExecutionException).Throw();

            if (!processed)
            {
                _dialogService.ShowWarning(
                    "Nothing was processed. Check the selected actions and try again.",
                    "Pending Transactions",
                    this);
                return;
            }

            _viewModel.DidProceed = true;
            Close();
        }
        catch (Exception exception)
        {
            _dialogService.ShowError(exception.Message, "Pending Transactions", this);
        }
        finally
        {
            _isProcessing = false;
        }
    }
}
