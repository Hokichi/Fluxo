using System.Windows;
using Fluxo.ViewModels.Popups;
using Fluxo.Views.CustomControls;

namespace Fluxo.Views.Popups;

public partial class TransferFundsPopup : BasePopup
{
    private readonly TransferFundsVM _viewModel;

    public TransferFundsPopup(TransferFundsVM viewModel)
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
            FluxoMessageBox.Show(this, result.ErrorMessage ?? "Unable to save this transfer.", "Transfer Funds",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Close();
    }

    protected override void OnApplyButtonClick()
    {
        base.OnCloseButtonClick();
    }
}
