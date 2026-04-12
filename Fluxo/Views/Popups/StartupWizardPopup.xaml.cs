using System.ComponentModel;
using System.Windows;
using Fluxo.Resources.CustomControls;
using Fluxo.ViewModels.Popups;

namespace Fluxo.Views.Popups;

public partial class StartupWizardPopup : BasePopup
{
    private readonly StartupWizardVM _viewModel;
    private bool _allowClose;
    private bool _isHandlingClose;

    public StartupWizardPopup(StartupWizardVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoadedAsync;
        Closing += OnClosingAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.LoadAsync();
        }
        catch (Exception exception)
        {
            FluxoMessageBox.Show(this, $"Unable to load the startup wizard.\n\n{exception.Message}",
                "Startup Wizard", MessageBoxButton.OK, MessageBoxImage.Information);
            _allowClose = true;
            Close();
        }
    }

    private async void OnClosingAsync(object? sender, CancelEventArgs e)
    {
        if (_allowClose || _isHandlingClose)
            return;

        _isHandlingClose = true;

        try
        {
            FluxoMessageBox.Show(this,
                "Setup isn't finished yet. Fluxo will continue to the app after this dialog.",
                "Startup Wizard",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            var result = await _viewModel.DismissAsync();
            if (!result.IsSuccess)
            {
                FluxoMessageBox.Show(this, result.ErrorMessage ?? "Unable to close the startup wizard.",
                    "Startup Wizard", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            _allowClose = true;
        }
        finally
        {
            _isHandlingClose = false;
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        _viewModel.GoBack();
    }

    private async void OnNextClick(object sender, RoutedEventArgs e)
    {
        var result = await _viewModel.GoNextAsync();
        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorMessage))
            FluxoMessageBox.Show(this, result.ErrorMessage, "Startup Wizard",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnFinishClick(object sender, RoutedEventArgs e)
    {
        var result = await _viewModel.CompleteAsync();
        if (!result.IsSuccess)
        {
            FluxoMessageBox.Show(this, result.ErrorMessage ?? "Unable to finish setup.", "Startup Wizard",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _allowClose = true;
        Close();
    }

    private async void OnAddSpendingSourceClick(object sender, RoutedEventArgs e)
    {
        new AddSpendingSourcePopup(_viewModel.CreateAddSpendingSourceViewModel()) { Owner = this }.ShowDialog();
        await _viewModel.RefreshSpendingSourcesAsync();
    }

    private async void OnAddFixedExpenseClick(object sender, RoutedEventArgs e)
    {
        new AddFixedExpensePopup(_viewModel.CreateAddFixedExpenseViewModel()) { Owner = this }.ShowDialog();
        await _viewModel.RefreshFixedExpensesAsync();
    }

    private async void OnAddSavingGoalClick(object sender, RoutedEventArgs e)
    {
        new AddSavingGoalPopup(_viewModel.CreateAddSavingGoalViewModel()) { Owner = this }.ShowDialog();
        await _viewModel.RefreshSavingGoalsAsync();
    }
}