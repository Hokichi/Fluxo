using System;
using System.Threading.Tasks;
using System.Windows;
using Fluxo.Services.Dialogs;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.Views.Popups;

public partial class AddTagPopup : BasePopup
{
    private readonly IDialogService _dialogService;
    private readonly Func<string, string, string, Task<SettingsOperationResult>> _saveTagAsync;
    private readonly AddTagVM _viewModel;

    public AddTagPopup(SettingsTagsTabVM settingsViewModel, IDialogService dialogService)
        : this(dialogService, settingsViewModel.CreateAddTagViewModel(), settingsViewModel.CreateTagAsync)
    {
    }

    public AddTagPopup(
        IDialogService dialogService,
        Func<string, string, string, Task<SettingsOperationResult>> saveTagAsync)
        : this(dialogService, new AddTagVM(), saveTagAsync)
    {
    }

    public AddTagPopup(
        IDialogService dialogService,
        AddTagVM viewModel,
        Func<string, string, string, Task<SettingsOperationResult>> saveTagAsync)
    {
        InitializeComponent();

        _dialogService = dialogService;
        _saveTagAsync = saveTagAsync ?? throw new ArgumentNullException(nameof(saveTagAsync));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        Loaded += (_, _) =>
        {
            _viewModel.BeginChangeTracking();
            TagNameTextBox.Focus();
        };
    }

    protected override async void OnSaveButtonClick()
    {
        var result = await _saveTagAsync(_viewModel.NameText, _viewModel.SelectedColorHex, _viewModel.SpendingLimitText);
        if (!result.IsSuccess)
        {
            ShowValidationMessage(result.ErrorMessage);
            return;
        }

        Close();
    }

    private void ShowValidationMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        FluxoMessageBox.Show(this, message, _viewModel.PopupTitle, MessageBoxButton.OK, MessageBoxImage.Information);
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

    private void OnAddCustomColorClick(object sender, RoutedEventArgs e)
    {
        var pickerResult = _dialogService.ShowAddTagColorPicker(_viewModel.SelectedColorHex, this);
        if (pickerResult.DialogResult == true && !string.IsNullOrWhiteSpace(pickerResult.SelectedHexColor))
            _viewModel.AddCustomColorToFront(pickerResult.SelectedHexColor);
    }
}
