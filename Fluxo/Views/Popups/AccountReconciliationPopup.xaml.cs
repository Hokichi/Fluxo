using System.Windows;
using Fluxo.ViewModels.Popups;
using Fluxo.Services.Notifications;
using Fluxo.Views.Shell.Main;

namespace Fluxo.Views.Popups;

public partial class AccountReconciliationPopup : BasePopup
{
    private readonly AccountReconciliationVM _viewModel;

    public AccountReconciliationPopup(AccountReconciliationVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => AmountTextBox.Focus();
    }

    protected override async void OnSaveButtonClick()
    {
        var result = await _viewModel.SaveAsync();
        if (!result.IsSuccess)
        {
            FloatingNotificationPublisher.SaveFailed(
                [result.ErrorMessage ?? "Unable to save this reconciliation."]);
            return;
        }

        var shouldModify = FluxoMessageBox.Show(
            this,
            "Would you like to modify this expense?",
            "Account Reconciliation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (shouldModify != MessageBoxResult.Yes || result.CreatedTransaction is null)
        {
            Close();
            return;
        }

        var ownerWindow = Owner as MainWindow;
        CloseForPopupHandoff();
        ownerWindow?.Dispatcher.BeginInvoke(new Action(() =>
            ownerWindow.OpenTransactionDetailPopupForEditing(result.CreatedTransaction)));
    }
}
