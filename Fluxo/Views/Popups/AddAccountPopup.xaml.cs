using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Popups;

namespace Fluxo.Views.Popups;

public partial class AddAccountPopup : BasePopup
{
    private readonly AddAccountVM _viewModel;

    public AddAccountPopup(AddAccountVM viewModel)
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
        if (HandleSaveFailure(result))
            return;

        if (result.ShouldClose)
            Close();
    }

    protected override async void OnSaveAndCreateNewButtonClick()
    {
        var result = await _viewModel.SaveAsync();
        if (HandleSaveFailure(result))
            return;

        _viewModel.ResetAfterSaveAndCreateNew();
        await _viewModel.LoadDeductSourcesAsync();
        FocusPrimaryInput();
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

    private bool HandleSaveFailure(AddAccountVM.AddAccountResult result)
    {
        if (result.IsSuccess)
            return false;

        if (result.FailurePresentation == AddAccountVM.AddAccountFailurePresentation.ToastWarning)
        {
            ShowWarningToast(result.ErrorMessage);
            return true;
        }

        ShowValidationMessage(result.ErrorMessage);
        return true;
    }

    private void FocusPrimaryInput()
    {
        NameTextBox.Focus();
    }

    private void OnNameTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _viewModel.ValidateNameField();
    }

    private void OnMaximumSpendingTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _viewModel.ValidateMaximumSpendingField();
    }

    private void OnSpentAmountTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _viewModel.ValidateSpentAmountField();
    }

    private void OnMinimumPaymentTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _viewModel.ValidateMinimumPaymentField();
    }

    private void OnApyTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _viewModel.ValidateApyField();
    }

    private void OnMaximumSpendingTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not FrameworkElement { IsKeyboardFocusWithin: true })
            return;

        _viewModel.MarkMaximumSpendingModified();
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
