using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.Services.Dialogs;
using Fluxo.ViewModels.Helpers;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.Views.CustomControls;

namespace Fluxo.Views.Popups;

public partial class AddFixedExpensePopup : BasePopup
{
    private readonly IDialogService _dialogService;
    private readonly SettingsTagsTabVM _settingsTagsTabViewModel;
    private readonly AddFixedExpenseVM _viewModel;
    private bool _isHandlingAddTagSelection;

    public AddFixedExpensePopup(
        AddFixedExpenseVM viewModel,
        IDialogService dialogService,
        SettingsTagsTabVM settingsTagsTabViewModel)
    {
        InitializeComponent();
        _dialogService = dialogService;
        _settingsTagsTabViewModel = settingsTagsTabViewModel;
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += async (_, _) =>
        {
            await _viewModel.LoadTagsAsync();
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
        if (!string.IsNullOrWhiteSpace(message))
            FluxoMessageBox.Show(this, message, _viewModel.ValidationDialogTitle, MessageBoxButton.OK,
                MessageBoxImage.Information);
    }

    private async void OnTagSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isHandlingAddTagSelection || _viewModel.SelectedTagOption?.IsAddTagAction != true)
            return;

        _isHandlingAddTagSelection = true;
        try
        {
            if (sender is ComboBox comboBox)
                comboBox.IsDropDownOpen = false;

            await OpenAddTagDialogAndRefreshAsync();
        }
        finally
        {
            _isHandlingAddTagSelection = false;
        }
    }

    private async Task OpenAddTagDialogAndRefreshAsync()
    {
        var previousTagNames = _viewModel.Tags
            .Select(tag => tag.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (_viewModel.IsDraftMode)
            _dialogService.ShowAddTag(_viewModel.CreateDraftTagAsync, this);
        else
            _dialogService.ShowAddTag(_settingsTagsTabViewModel, this);

        await _viewModel.LoadTagsAsync();

        var newTag = _viewModel.Tags.FirstOrDefault(tag =>
            !string.IsNullOrWhiteSpace(tag.Name) &&
            !previousTagNames.Contains(tag.Name));

        if (newTag is not null)
            _viewModel.SelectedTag = newTag;
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
