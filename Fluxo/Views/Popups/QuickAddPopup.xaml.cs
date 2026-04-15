using System.ComponentModel;
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
    private bool _allowClose;
    private bool _isHandlingCloseRequest;
    private bool _isSyncingNoteDocument;

    public QuickAddPopup(QuickAddVM viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += (_, _) =>
        {
            SyncNoteDocumentFromViewModel();
            FocusPrimaryInput();
        };
        Closing += OnPopupClosing;
    }

    protected override async void OnSaveButtonClick()
    {
        var result = await _viewModel.SaveAsync(false);
        if (!result.IsSuccess)
        {
            ShowValidationMessage(result.ErrorMessage);
            return;
        }

        _allowClose = true;
        Close();
    }

    protected override async void OnSaveAndCreateNewButtonClick()
    {
        var result = await _viewModel.SaveAsync(true);
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
        if (e.Key == Key.Enter && NoteRichTextBox.IsKeyboardFocusWithin && Keyboard.Modifiers != ModifierKeys.Shift)
            return;

        base.OnPreviewKeyDown(e);
    }

    private async void OnPopupClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || _isHandlingCloseRequest || !_viewModel.HasValidEntryToPersistOnClose())
            return;

        _isHandlingCloseRequest = true;

        try
        {
            var confirmation = FluxoMessageBox.Show(
                this,
                "Save this transaction before closing?",
                "Add New Transaction",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation == MessageBoxResult.Yes)
            {
                var result = await _viewModel.SaveAsync(false);
                if (!result.IsSuccess)
                {
                    ShowValidationMessage(result.ErrorMessage);
                    return;
                }
            }

            _allowClose = true;
        }
        finally
        {
            _isHandlingCloseRequest = false;
        }
    }

    private void OnNoteTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncingNoteDocument)
            return;

        _viewModel.NoteText = new TextRange(NoteRichTextBox.Document.ContentStart, NoteRichTextBox.Document.ContentEnd)
            .Text
            .Trim();
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
            ExpenseAmountTextBox.Focus();
            return;
        }

        IncomeAmountTextBox.Focus();
    }

    private void SyncNoteDocumentFromViewModel()
    {
        _isSyncingNoteDocument = true;

        try
        {
            var noteText = _viewModel.NoteText ?? string.Empty;
            new TextRange(NoteRichTextBox.Document.ContentStart, NoteRichTextBox.Document.ContentEnd).Text = noteText;
        }
        finally
        {
            _isSyncingNoteDocument = false;
        }
    }

}
