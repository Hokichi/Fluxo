using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.ViewModels.Helpers;
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

    private void OnMonthlyDueDatePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
            e.Handled = true;
    }

    private void OnMonthlyDueDatePreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox textBox)
            e.Handled = !WouldResultInValidMonthlyDueDate(textBox, e.Text);
    }

    private void OnMonthlyDueDatePasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        if (!e.SourceDataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.SourceDataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        if (!WouldResultInValidMonthlyDueDate(textBox, pastedText))
            e.CancelCommand();
    }

    private static bool WouldResultInValidMonthlyDueDate(TextBox textBox, string incomingText)
    {
        if (string.IsNullOrEmpty(incomingText))
            return true;

        foreach (var character in incomingText)
            if (!char.IsDigit(character))
                return false;

        var currentText = textBox.Text ?? string.Empty;
        var nextText = currentText
            .Remove(textBox.SelectionStart, textBox.SelectionLength)
            .Insert(textBox.SelectionStart, incomingText);

        if (nextText.Length == 0)
            return true;

        if (!int.TryParse(nextText, out var monthlyDueDate))
            return false;

        return monthlyDueDate is >= MonthlyDueDateHelper.MinMonthlyDay and <= MonthlyDueDateHelper.MaxMonthlyDay;
    }
}