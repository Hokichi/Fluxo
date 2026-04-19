using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Fluxo.ViewModels.Popups;
using Fluxo.Views.CustomControls;
using Fluxo.Views.Shell;
using Fluxo.Views.Shell.Main;

namespace Fluxo.Views.Popups;

public partial class ExpenseDetailPopup : BasePopup
{
    private readonly ExpenseDetailVM _viewModel;
    private bool _allowClose;
    private bool _isHandlingCloseRequest;
    private bool _isSyncingNoteDocument;

    public ExpenseDetailPopup(ExpenseDetailVM viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExpenseDetailVM.PopupTitle))
                PopupTitle = _viewModel.PopupTitle;

            if (e.PropertyName == nameof(ExpenseDetailVM.IsEditing))
                UpdateButtonStates();
        };

        Loaded += (_, _) =>
        {
            SyncNoteDocumentFromViewModel();
            UpdateButtonStates();
        };
        Closing += OnPopupClosing;
    }

    protected override void OnEditButtonClick()
    {
        _viewModel.BeginEditing();
        ExpenseNameTextBox.Focus();
    }

    protected override async void OnSaveButtonClick()
    {
        var result = await _viewModel.SaveAsync();
        if (!result.IsSuccess)
        {
            ShowValidationMessage(result.ErrorMessage);
            return;
        }

        SyncNoteDocumentFromViewModel();
    }

    protected override void OnCloneButtonClick()
    {
        var draft = _viewModel.CreateQuickAddDraft();
        var ownerWindow = Owner as MainWindow;

        Close();

        ownerWindow?.Dispatcher.BeginInvoke(new Action(() => ownerWindow.OpenAddNewTransactionPopup(draft)));
    }

    protected override void OnCancelButtonClick()
    {
        _viewModel.CancelEditing();
        SyncNoteDocumentFromViewModel();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && NoteRichTextBox.IsKeyboardFocusWithin)
            return;

        base.OnPreviewKeyDown(e);
    }

    private async void OnPopupClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || _isHandlingCloseRequest || !_viewModel.HasValidChangesToPersistOnClose())
            return;

        e.Cancel = true;
        _isHandlingCloseRequest = true;

        try
        {
            var confirmation = FluxoMessageBox.Show(
                this,
                "Save your changes before closing?",
                "Expense Detail",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation == MessageBoxResult.Yes)
            {
                var result = await _viewModel.SaveAsync();
                if (!result.IsSuccess)
                {
                    ShowValidationMessage(result.ErrorMessage);
                    return;
                }

                SyncNoteDocumentFromViewModel();
            }

            _allowClose = true;
            _ = Dispatcher.BeginInvoke(new System.Action(Close));
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

    private void UpdateButtonStates()
    {
        ShowEditButton = !_viewModel.IsEditing;
        ShowSaveButton = _viewModel.IsEditing;
        ShowCloneButton = !_viewModel.IsEditing;
        ShowCancelButton = _viewModel.IsEditing;
    }

    private void ShowValidationMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        FluxoMessageBox.Show(this, message, "Expense Detail", MessageBoxButton.OK, MessageBoxImage.Information);
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
