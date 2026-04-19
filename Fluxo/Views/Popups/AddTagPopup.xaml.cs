using System.Windows;
using Fluxo.Services.Dialogs;
using Fluxo.ViewModels.Popups;
using Fluxo.Views.CustomControls;

namespace Fluxo.Views.Popups;

public partial class AddTagPopup : BasePopup
{
    private readonly IDialogService _dialogService;
    private readonly SettingsVM _settingsViewModel;
    private readonly AddTagVM _viewModel;

    public AddTagPopup(SettingsVM settingsViewModel, IDialogService dialogService)
    {
        InitializeComponent();

        _dialogService = dialogService;
        _settingsViewModel = settingsViewModel;
        _viewModel = new AddTagVM();
        DataContext = _viewModel;

        Loaded += (_, _) => TagNameTextBox.Focus();
    }

    protected override async void OnSaveButtonClick()
    {
        var result = await _settingsViewModel.CreateTagAsync(_viewModel.NameText, _viewModel.SelectedColorHex);
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

        FluxoMessageBox.Show(this, message, "Add New Tag", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnAddCustomColorClick(object sender, RoutedEventArgs e)
    {
        var pickerResult = _dialogService.ShowAddTagColorPicker(_viewModel.SelectedColorHex, this);
        if (pickerResult.DialogResult == true && !string.IsNullOrWhiteSpace(pickerResult.SelectedHexColor))
            _viewModel.AddCustomColorToFront(pickerResult.SelectedHexColor);
    }
}
