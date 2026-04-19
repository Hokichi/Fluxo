using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.ViewModels.Helpers;
using Fluxo.ViewModels.Popups;
using Fluxo.Views.CustomControls;

namespace Fluxo.Views.Popups;

public partial class AddFixedExpensePopup : BasePopup
{
    private readonly AddFixedExpenseVM _viewModel;

    public AddFixedExpensePopup(AddFixedExpenseVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
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
                "Close without saving your changes?",
                "Add Fixed Expense",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
                return;
        }

        base.OnCloseButtonClick();
    }

    private void ShowValidationMessage(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            FluxoMessageBox.Show(this, message, "Add Fixed Expense", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnRecurringDatePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
            e.Handled = true;
    }

    private void OnRecurringDatePreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox textBox)
            e.Handled = !WouldResultInValidRecurringDate(textBox, e.Text);
    }

    private void OnRecurringDatePasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        if (!e.SourceDataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.SourceDataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        if (!WouldResultInValidRecurringDate(textBox, pastedText))
            e.CancelCommand();
    }

    private static bool WouldResultInValidRecurringDate(TextBox textBox, string incomingText)
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

        if (!int.TryParse(nextText, out var recurringDate))
            return false;

        return recurringDate is >= MonthlyDueDateHelper.MinMonthlyDay and <= MonthlyDueDateHelper.MaxMonthlyDay;
    }
}
