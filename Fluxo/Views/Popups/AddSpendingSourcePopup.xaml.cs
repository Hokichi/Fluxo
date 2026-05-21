using System.Windows;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Popups;

namespace Fluxo.Views.Popups;

public partial class AddSpendingSourcePopup : BasePopup
{
    private readonly AddSpendingSourceVM _viewModel;

    public AddSpendingSourcePopup(AddSpendingSourceVM viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += async (_, _) =>
        {
            await _viewModel.LoadDeductSourcesAsync();
            _viewModel.BeginChangeTracking();
            NameTextBox.Focus();
        };
    }

    protected override async void OnSaveButtonClick()
    {
        var result = await _viewModel.SaveAsync();
        if (!result.IsSuccess)
        {
            if (result.FailurePresentation == AddSpendingSourceVM.AddSpendingSourceFailurePresentation.ToastWarning)
            {
                ShowWarningToast(result.ErrorMessage);
                return;
            }

            ShowValidationMessage(result.ErrorMessage);
            return;
        }

        if (result.ShouldClose)
            Close();
    }

    protected override void OnCloseButtonClick()
    {
        if (_viewModel.HasChanges)
        {
            var confirmation = FluxoMessageBox.Show(this,
                "Discard all changes?",
                _viewModel.PopupTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
                return;
        }

        base.OnCloseButtonClick();
    }

    private void ShowValidationMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        FluxoMessageBox.Show(this, message, _viewModel.ValidationDialogTitle, MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ShowWarningToast(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var popup = new ToastPopup(
            message,
            () => Task.Delay(1800),
            NotificationSeverity.Warning)
        {
            Owner = this
        };

        popup.Show();
    }

}
