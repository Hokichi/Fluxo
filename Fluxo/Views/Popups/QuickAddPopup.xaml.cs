using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Fluxo.Resources.CustomControls;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell;

namespace Fluxo.Views.Popups;

public partial class QuickAddPopup : BasePopup
{
    private readonly QuickAddVM _viewModel;

    public QuickAddPopup(MainVM mainViewModel)
    {
        InitializeComponent();

        var app = (App)Application.Current;
        _viewModel = new QuickAddVM(mainViewModel, () => app.GetRequiredService<Fluxo.Core.Interfaces.IUnitOfWork>());
        DataContext = _viewModel;

        Loaded += (_, _) => AmountTextBox.Focus();
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
        AmountTextBox.Focus();
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

    private void ShowValidationMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        MessageBox.Show(this, message, "Add New Transaction", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
