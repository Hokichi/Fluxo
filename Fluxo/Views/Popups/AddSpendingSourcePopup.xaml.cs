using System.Windows;
using Fluxo.Resources.CustomControls;
using Fluxo.ViewModels.Popups;

namespace Fluxo.Views.Popups;

public partial class AddSpendingSourcePopup : BasePopup
{
    private readonly AddSpendingSourceVM _viewModel;

    public AddSpendingSourcePopup(AddSpendingSourceVM viewModel)
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
                "Close without saving your changes?",
                "Add New Income Source",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
                return;
        }

        base.OnCloseButtonClick();
    }

    private void ShowValidationMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        FluxoMessageBox.Show(this, message, "Add New Income Source", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
