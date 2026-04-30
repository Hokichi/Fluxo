using System.Windows;
using Fluxo.ViewModels.Popups;
using Fluxo.Views.CustomControls;

namespace Fluxo.Views.Popups;

public partial class AddSavingGoalPopup : BasePopup
{
    private readonly AddSavingGoalVM _viewModel;

    public AddSavingGoalPopup(AddSavingGoalVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
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
}
