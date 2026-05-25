using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Fluxo.ViewModels.Popups;
using Fluxo.Views.Shell.Main;

namespace Fluxo.Views.Popups;

public partial class IncomeDetailPopup : BasePopup
{
    private readonly IncomeDetailVM _viewModel;
    private bool _isSyncingNoteDocument;

    public IncomeDetailPopup(IncomeDetailVM viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IncomeDetailVM.PopupTitle))
                PopupTitle = _viewModel.PopupTitle;
        };

        Loaded += (_, _) =>
        {
            SyncNoteDocumentFromViewModel();
        };
    }

    protected override void OnEditButtonClick()
    {
        OpenEditorPopupAndClose();
    }

    protected override void OnCloneButtonClick()
    {
        OpenEditorPopupAndClose();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && NoteRichTextBox.IsKeyboardFocusWithin)
            return;

        base.OnPreviewKeyDown(e);
    }

    private void OnNoteTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncingNoteDocument)
            return;

        _viewModel.NoteText = new TextRange(NoteRichTextBox.Document.ContentStart, NoteRichTextBox.Document.ContentEnd)
            .Text
            .Trim();
    }

    private void OpenEditorPopupAndClose()
    {
        var ownerWindow = Owner as MainWindow;
        if (ownerWindow is null)
            return;

        var draft = _viewModel.CreateQuickAddDraft();
        CloseForPopupHandoff();
        ownerWindow.Dispatcher.BeginInvoke(new Action(() => ownerWindow.OpenAddNewTransactionPopup(draft)));
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
