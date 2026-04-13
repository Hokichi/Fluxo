using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Fluxo.Resources.CustomControls;
using Fluxo.ViewModels.Popups;

namespace Fluxo.Views.Popups;

public partial class StartupWizardPopup : BasePopup
{
    private static readonly Duration FadeDuration = new(TimeSpan.FromMilliseconds(150));

    private readonly StartupWizardVM _viewModel;
    private bool _allowClose;
    private bool _isAnimating;
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

    private async void OnBackClick(object sender, RoutedEventArgs e)
    {
        await AnimateStepTransitionAsync(_viewModel.GoBack);
    }

    private async void OnNextClick(object sender, RoutedEventArgs e)
    {
        var result = await AnimateStepTransitionAsync(_viewModel.GoNextAsync);
        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            FluxoMessageBox.Show(this, result.ErrorMessage, "Startup Wizard",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_viewModel.IsLoadingStep)
            await RunLoadingStepAsync();
    }

    private async Task RunLoadingStepAsync()
    {
        try
        {
            await _viewModel.InitializeMainViewModelAsync();
        }
        catch (Exception exception)
        {
            FluxoMessageBox.Show(this, $"Unable to load data.\n\n{exception.Message}",
                "Startup Wizard", MessageBoxButton.OK, MessageBoxImage.Information);
            _allowClose = true;
            Close();
            return;
        }

        await AnimateStepTransitionAsync(_viewModel.GoNextAsync);
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

    private async void OnEditSpendingSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id })
            return;

        var vm = await _viewModel.CreateEditSpendingSourceViewModelAsync(id);
        new AddSpendingSourcePopup(vm) { Owner = this }.ShowDialog();
        await _viewModel.RefreshSpendingSourcesAsync();
    }

    private async void OnDeleteSpendingSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id })
            return;

        var result = FluxoMessageBox.Show(this,
            "Are you sure you want to delete this spending source?",
            "Delete Spending Source", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            await _viewModel.DeleteSpendingSourceAsync(id);
    }

    private async void OnEditFixedExpenseClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id })
            return;

        var vm = await _viewModel.CreateEditFixedExpenseViewModelAsync(id);
        new AddFixedExpensePopup(vm) { Owner = this }.ShowDialog();
        await _viewModel.RefreshFixedExpensesAsync();
    }

    private async void OnDeleteFixedExpenseClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id })
            return;

        var result = FluxoMessageBox.Show(this,
            "Are you sure you want to delete this fixed expense?",
            "Delete Fixed Expense", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            await _viewModel.DeleteFixedExpenseAsync(id);
    }

    private async void OnEditSavingGoalClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id })
            return;

        var vm = await _viewModel.CreateEditSavingGoalViewModelAsync(id);
        new AddSavingGoalPopup(vm) { Owner = this }.ShowDialog();
        await _viewModel.RefreshSavingGoalsAsync();
    }

    private async void OnDeleteSavingGoalClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id })
            return;

        var result = FluxoMessageBox.Show(this,
            "Are you sure you want to delete this saving goal?",
            "Delete Saving Goal", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            await _viewModel.DeleteSavingGoalAsync(id);
    }

    private async void OnDotClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: WizardStepDotVM dot })
            return;

        var targetStep = dot.StepIndex;
        if (targetStep == _viewModel.CurrentStepIndex)
            return;

        await AnimateStepTransitionAsync(() => _viewModel.NavigateToStep(targetStep));
    }

    private Border? GetStripeForStep(int stepIndex) => stepIndex switch
    {
        1 => Step1Stripe,
        2 => Step2Stripe,
        3 => Step3Stripe,
        4 => Step4Stripe,
        5 => Step5Stripe,
        6 => Step6Stripe,
        7 => Step7Stripe,
        _ => null
    };

    private bool IsMiddleStep(int stepIndex) => stepIndex >= 1 && stepIndex <= 7;

    private async Task AnimateStepTransitionAsync(Action changeStep)
    {
        if (_isAnimating)
            return;

        _isAnimating = true;

        try
        {
            var fromStep = _viewModel.CurrentStepIndex;

            if (IsCrossSectionTransition(fromStep, isForward: false))
            {
                await FadeElementAsync(ContentContainer, 1, 0);
                changeStep();
                SyncStripeOpacities();
                await FadeElementAsync(ContentContainer, 0, 1);
            }
            else
            {
                var oldStripe = GetStripeForStep(fromStep);
                await Task.WhenAll(
                    FadeElementAsync(ContentColumn, 1, 0),
                    oldStripe is not null ? FadeElementAsync(oldStripe, 1, 0) : Task.CompletedTask);

                changeStep();
                var newStripe = GetStripeForStep(_viewModel.CurrentStepIndex);
                SyncStripeOpacities();

                await Task.WhenAll(
                    FadeElementAsync(ContentColumn, 0, 1),
                    newStripe is not null ? FadeElementAsync(newStripe, 0, 1) : Task.CompletedTask);
            }
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private async Task<SettingsOperationResult> AnimateStepTransitionAsync(
        Func<Task<SettingsOperationResult>> changeStepAsync)
    {
        if (_isAnimating)
            return SettingsOperationResult.Success();

        _isAnimating = true;

        try
        {
            var fromStep = _viewModel.CurrentStepIndex;

            if (IsCrossSectionTransition(fromStep, isForward: true))
            {
                await FadeElementAsync(ContentContainer, 1, 0);
                var result = await changeStepAsync();
                SyncStripeOpacities();
                await FadeElementAsync(ContentContainer, 0, 1);
                return result;
            }
            else
            {
                var oldStripe = GetStripeForStep(fromStep);
                await Task.WhenAll(
                    FadeElementAsync(ContentColumn, 1, 0),
                    oldStripe is not null ? FadeElementAsync(oldStripe, 1, 0) : Task.CompletedTask);

                var result = await changeStepAsync();
                var newStripe = GetStripeForStep(_viewModel.CurrentStepIndex);
                SyncStripeOpacities();

                await Task.WhenAll(
                    FadeElementAsync(ContentColumn, 0, 1),
                    newStripe is not null ? FadeElementAsync(newStripe, 0, 1) : Task.CompletedTask);

                return result;
            }
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private bool IsCrossSectionTransition(int fromStep, bool isForward)
    {
        var toStep = isForward ? fromStep + 1 : fromStep - 1;
        return !(IsMiddleStep(fromStep) && IsMiddleStep(toStep));
    }

    private void SyncStripeOpacities()
    {
        for (var i = 1; i <= 7; i++)
        {
            var stripe = GetStripeForStep(i);
            if (stripe is null) continue;
            stripe.BeginAnimation(OpacityProperty, null);
            stripe.Opacity = i == _viewModel.CurrentStepIndex ? 1 : 0;
        }
    }

    private Task FadeElementAsync(UIElement element, double from, double to)
    {
        var tcs = new TaskCompletionSource();
        var animation = new DoubleAnimation(from, to, FadeDuration)
        {
            EasingFunction = new QuadraticEase
            {
                EasingMode = to < from ? EasingMode.EaseIn : EasingMode.EaseOut
            }
        };
        animation.Completed += (_, _) => tcs.SetResult();
        element.BeginAnimation(OpacityProperty, animation);
        return tcs.Task;
    }
}
