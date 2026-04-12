using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.Resources.CustomControls;
using Fluxo.ViewModels.Popups;

namespace Fluxo.Views.Popups;

public partial class AddSavingGoalPopup : BasePopup
{
    private readonly AddSavingGoalVM _viewModel;

    public AddSavingGoalPopup(AddSavingGoalVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => NameTextBox.Focus();
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

    private void OnAmountTextBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        e.Handled = !IsValidAmountInput(textBox, e.Text);
    }

    private void ShowValidationMessage(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            FluxoMessageBox.Show(this, message, "Add Goal", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static bool IsValidAmountInput(TextBox textBox, string newText)
    {
        if (string.IsNullOrEmpty(newText))
            return true;

        var proposedText = GetProposedText(textBox, newText);
        if (string.IsNullOrWhiteSpace(proposedText))
            return true;

        var separators = GetAllowedDecimalSeparators();
        var separatorCount = 0;

        foreach (var character in proposedText)
        {
            if (char.IsDigit(character))
                continue;

            if (!separators.Contains(character))
                return false;

            separatorCount++;
            if (separatorCount > 1)
                return false;
        }

        return true;
    }

    private static string GetProposedText(TextBox textBox, string newText)
    {
        var existingText = textBox.Text ?? string.Empty;
        var selectionStart = textBox.SelectionStart;
        var selectionLength = textBox.SelectionLength;

        if (selectionLength > 0)
            return existingText.Remove(selectionStart, selectionLength).Insert(selectionStart, newText);

        return existingText.Insert(selectionStart, newText);
    }

    private static HashSet<char> GetAllowedDecimalSeparators()
    {
        var separators = new HashSet<char> { '.' };
        var currentSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

        foreach (var character in currentSeparator)
            separators.Add(character);

        return separators;
    }
}
