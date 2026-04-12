using System.Windows;
using Fluxo.Resources.CustomControls;
using Fluxo.ViewModels.Popups;

namespace Fluxo.Views.Popups;

public partial class AddTagPopup : BasePopup
{
    private readonly SettingsVM _settingsViewModel;
    private readonly AddTagVM _viewModel;

    public AddTagPopup(SettingsVM settingsViewModel)
    {
        InitializeComponent();

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
}
