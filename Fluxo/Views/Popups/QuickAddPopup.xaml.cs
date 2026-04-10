using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Fluxo.Resources.CustomControls;
using Fluxo.ViewModels.Popups;

namespace Fluxo.Views.Popups;

public partial class QuickAddPopup : BasePopup
{
    private readonly QuickAddVM _viewModel;

    public QuickAddPopup(QuickAddVM viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += (_, _) => FocusPrimaryInput();
    }

    protected override async void OnSaveButtonClick()
    {
        var result = await _viewModel.SaveAsync(resetAfterSave: false);
        if (!result.IsSuccess)
        {
            ShowValidationMessage(result.ErrorMessage);
            return;
        }

        Close();
    }

    protected override async void OnSaveAndCreateNewButtonClick()
    {
        var result = await _viewModel.SaveAsync(resetAfterSave: true);
        if (!result.IsSuccess)
        {
            ShowValidationMessage(result.ErrorMessage);
            return;
        }

        NoteRichTextBox.Document.Blocks.Clear();
        FocusPrimaryInput();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && NoteRichTextBox.IsKeyboardFocusWithin)
            return;

        base.OnPreviewKeyDown(e);
    }

    private void OnNoteTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _viewModel.NoteText = new TextRange(NoteRichTextBox.Document.ContentStart, NoteRichTextBox.Document.ContentEnd)
            .Text
            .Trim();
    }

    private void OnAmountTextBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        e.Handled = !IsValidAmountInput(textBox, e.Text);
    }

    private void OnAmountTextBoxPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        if (!e.SourceDataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.SourceDataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        if (!IsValidAmountInput(textBox, pastedText))
            e.CancelCommand();
    }

    private void ShowValidationMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        FluxoMessageBox.Show(this, message, "Add New Transaction", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void FocusPrimaryInput()
    {
        if (_viewModel.IsExpense)
        {
            ExpenseNameTextBox.Focus();
            return;
        }

        IncomeNameTextBox.Focus();
    }

    private static bool IsValidAmountInput(TextBox textBox, string newText)
    {
        if (string.IsNullOrEmpty(newText))
            return true;

        var proposedText = GetProposedText(textBox, newText);
        if (string.IsNullOrWhiteSpace(proposedText))
            return true;

        var decimalSeparators = GetAllowedDecimalSeparators();
        var separatorCount = 0;

        foreach (var character in proposedText)
        {
            if (char.IsDigit(character))
                continue;

            if (!decimalSeparators.Contains(character))
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
