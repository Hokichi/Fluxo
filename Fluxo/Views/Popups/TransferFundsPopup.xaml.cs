using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.Resources.CustomControls;
using Fluxo.ViewModels.Popups;

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

    private void OnAmountTextBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        e.Handled = !IsValidAmountInput(textBox, e.Text);
    }

    protected override void OnApplyButtonClick()
    {
        base.OnCloseButtonClick();
    }

    private static bool IsValidAmountInput(TextBox textBox, string newText)
    {
        var proposedText = textBox.SelectionLength > 0
            ? (textBox.Text ?? string.Empty).Remove(textBox.SelectionStart, textBox.SelectionLength)
            .Insert(textBox.SelectionStart, newText)
            : (textBox.Text ?? string.Empty).Insert(textBox.SelectionStart, newText);

        if (string.IsNullOrWhiteSpace(proposedText))
            return true;

        var separators = new HashSet<char> { '.', CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0] };
        var separatorCount = 0;
        foreach (var character in proposedText)
        {
            if (char.IsDigit(character))
                continue;
            if (!separators.Contains(character))
                return false;
            if (++separatorCount > 1)
                return false;
        }

        return true;
    }
}