using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Fluxo.ViewModels.Popups;
using Fluxo.Views.CustomControls;

namespace Fluxo.Views.Popups;

public partial class AddNewTransaction : BasePopup
{
    private readonly QuickAddVM _viewModel;
    private bool _isSyncingNoteDocument;

    public AddNewTransaction(QuickAddVM viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += async (_, _) =>
        {
            await _viewModel.EnsureTagsLoadedAsync();
            SyncNoteDocumentFromViewModel();
            _viewModel.BeginChangeTracking();
            FocusPrimaryInput();
        };
    }

    protected override async void OnSaveButtonClick()
    {
        var result = await _viewModel.SaveAsync(false);
        if (!result.IsSuccess)
        {
            ShowValidationMessage(result.ErrorMessage);
            return;
        }

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
        _viewModel.BeginChangeTracking();
        FocusPrimaryInput();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && NoteRichTextBox.IsKeyboardFocusWithin && Keyboard.Modifiers != ModifierKeys.Shift)
            return;

        base.OnPreviewKeyDown(e);
    }

    protected override void OnCloseButtonClick()
    {
        if (_viewModel.HasChanges)
        {
            var confirmation = FluxoMessageBox.Show(
                this,
                "Close without saving your changes?",
                "Add New Transaction",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
                return;
        }

        base.OnCloseButtonClick();
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
        if (_viewModel.IsGoal)
        {
            GoalAmountTextBox.Focus();
            return;
        }

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
