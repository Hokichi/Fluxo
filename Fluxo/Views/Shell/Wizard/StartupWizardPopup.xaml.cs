using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Fluxo.Core.Enums;
using Fluxo.Services.Dialogs;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.Views.CustomControls;
using StartupWizardVM = Fluxo.ViewModels.Shell.StartupWizard.StartupWizardVM;
using WizardStepDotVM = Fluxo.ViewModels.Shell.StartupWizard.StartupWizardStepDotVM;

namespace Fluxo.Views.Shell.Wizard;

public partial class StartupWizardPopup : BasePopup
{
    private static readonly Duration FadeDuration = new(TimeSpan.FromMilliseconds(150));

    private readonly IDialogService _dialogService;
    private readonly StartupWizardVM _viewModel;
    private bool _allowClose;
    private bool _isAnimating;
    private bool _isHandlingClose;
    private readonly DispatcherTimer _allocationHoldDelayTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _allocationRepeatTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private int _heldAllocationDelta;
    private BudgetAllocationSegment _heldAllocationSegment;

    public StartupWizardPopup(StartupWizardVM viewModel, IDialogService dialogService)
    {
        InitializeComponent();
        _dialogService = dialogService;
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoadedAsync;
        Closing += OnClosingAsync;
        _allocationHoldDelayTimer.Tick += OnAllocationHoldDelayTick;
        _allocationRepeatTimer.Tick += OnAllocationRepeatTick;
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
        if (_allowClose)
            return;

        if (_isHandlingClose)
        {
            e.Cancel = true;
            return;
        }

        e.Cancel = true;
        _isHandlingClose = true;

        try
        {
            var confirmation = FluxoMessageBox.Show(this,
                "Setup isn't finished yet. Fluxo will continue to the app after this dialog.",
                "Startup Wizard",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
                return;

            var result = await _viewModel.DismissAsync();
            if (!result.IsSuccess)
            {
                FluxoMessageBox.Show(this, result.ErrorMessage ?? "Unable to close the startup wizard.",
                    "Startup Wizard", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _allowClose = true;
            _ = Dispatcher.BeginInvoke(new System.Action(Close));
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
        if (_viewModel.IsStep2Active && !_viewModel.HasSpendingSources)
        {
            var dialogResult = FluxoMessageBox.Show(this,
                "A spending source is required to calculate and if there are no available sources, Fixed expenses and Saving goals setup will be skipped. Do you want to continue without adding a source?",
                "Startup Wizard",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (dialogResult == MessageBoxResult.Yes)
            {
                await AnimateStepTransitionAsync(() =>
                {
                    _viewModel.NavigateToStep(5);
                });
                return;
            }
            else
            {
                OnAddSpendingSourceClick(sender, e);
                return;
            }
        }

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

    public async void OnAddSpendingSourceClick(object sender, RoutedEventArgs e)
    {
        _dialogService.ShowAddSpendingSource(_viewModel.MiddlePage.SpendingSources.CreateAddViewModel(), this);
        await _viewModel.MiddlePage.SpendingSources.RefreshAsync();
    }

    public async void OnAddFixedExpenseClick(object sender, RoutedEventArgs e)
    {
        _dialogService.ShowAddFixedExpense(_viewModel.MiddlePage.FixedExpenses.CreateAddViewModel(), this);
        await _viewModel.MiddlePage.FixedExpenses.RefreshAsync();
    }

    public async void OnAddSavingGoalClick(object sender, RoutedEventArgs e)
    {
        _dialogService.ShowAddSavingGoal(_viewModel.MiddlePage.SavingGoals.CreateAddViewModel(), this);
        await _viewModel.MiddlePage.SavingGoals.RefreshAsync();
    }

    public async void OnEditSpendingSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id })
            return;

        var vm = await _viewModel.MiddlePage.SpendingSources.CreateEditViewModelAsync(id);
        _dialogService.ShowAddSpendingSource(vm, this);
        await _viewModel.MiddlePage.SpendingSources.RefreshAsync();
    }

    public async void OnDeleteSpendingSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id })
            return;

        var result = FluxoMessageBox.Show(this,
            "Are you sure you want to delete this spending source?",
            "Delete Spending Source", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            await _viewModel.MiddlePage.SpendingSources.DeleteAsync(id);
    }

    public async void OnEditFixedExpenseClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id })
            return;

        var vm = await _viewModel.MiddlePage.FixedExpenses.CreateEditViewModelAsync(id);
        _dialogService.ShowAddFixedExpense(vm, this);
        await _viewModel.MiddlePage.FixedExpenses.RefreshAsync();
    }

    public async void OnDeleteFixedExpenseClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id })
            return;

        var result = FluxoMessageBox.Show(this,
            "Are you sure you want to delete this fixed expense?",
            "Delete Fixed Expense", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            await _viewModel.MiddlePage.FixedExpenses.DeleteAsync(id);
    }

    public async void OnEditSavingGoalClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id })
            return;

        var vm = await _viewModel.MiddlePage.SavingGoals.CreateEditViewModelAsync(id);
        _dialogService.ShowAddSavingGoal(vm, this);
        await _viewModel.MiddlePage.SavingGoals.RefreshAsync();
    }

    public async void OnDeleteSavingGoalClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id })
            return;

        var result = FluxoMessageBox.Show(this,
            "Are you sure you want to delete this saving goal?",
            "Delete Saving Goal", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            await _viewModel.MiddlePage.SavingGoals.DeleteAsync(id);
    }

    private async void OnDotClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: WizardStepDotVM dot })
            return;

        var targetStep = dot.StepIndex;
        if (targetStep == _viewModel.CurrentStepIndex)
            return;

        if ((targetStep == 3 || targetStep == 4) && !_viewModel.HasSpendingSources)
            return;

        await AnimateStepTransitionAsync(() => _viewModel.NavigateToStep(targetStep));
    }

    public void OnAllocationAdjustButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag } ||
            !TryParseAllocationTag(tag, out var segment, out var delta))
            return;
        _viewModel.MiddlePage.BudgetAllocation.IncrementAllocation(segment, delta);
    }

    public void OnAllocationAdjustButtonMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag } ||
            !TryParseAllocationTag(tag, out var segment, out var delta))
            return;
        _heldAllocationSegment = segment;
        _heldAllocationDelta = delta;
        _allocationRepeatTimer.Stop();
        _allocationHoldDelayTimer.Stop();
        _allocationHoldDelayTimer.Start();
    }

    public void OnAllocationAdjustButtonMouseUp(object sender, MouseButtonEventArgs e)
    {
        StopAllocationTimers();
    }

    public void OnAllocationAdjustButtonMouseLeave(object sender, MouseEventArgs e)
    {
        StopAllocationTimers();
    }

    private void OnAllocationHoldDelayTick(object? sender, EventArgs e)
    {
        _allocationHoldDelayTimer.Stop();
        _allocationRepeatTimer.Start();
    }

    private void OnAllocationRepeatTick(object? sender, EventArgs e)
    {
        _viewModel.MiddlePage.BudgetAllocation.IncrementAllocation(_heldAllocationSegment, _heldAllocationDelta);
    }

    private void StopAllocationTimers()
    {
        _allocationHoldDelayTimer.Stop();
        _allocationRepeatTimer.Stop();
    }

    private static bool TryParseAllocationTag(string tag, out BudgetAllocationSegment segment, out int delta)
    {
        segment = default;
        delta = 0;
        var parts = tag.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Enum.TryParse(parts[0], out segment))
            return false;
        delta = string.Equals(parts[1], "+1", StringComparison.Ordinal) ? 1 : -1;
        return true;
    }

    private Border? GetStripeForStep(int stepIndex) => MiddleStepPage?.GetStripeForStep(stepIndex);

    private bool IsMiddleStep(int stepIndex) => stepIndex >= 2 && stepIndex <= 7;

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
                var contentColumn = MiddleStepPage?.ContentColumnElement;
                await Task.WhenAll(
                    contentColumn is not null ? FadeElementAsync(contentColumn, 1, 0) : Task.CompletedTask,
                    oldStripe is not null ? FadeElementAsync(oldStripe, 1, 0) : Task.CompletedTask);

                changeStep();
                var newStripe = GetStripeForStep(_viewModel.CurrentStepIndex);
                SyncStripeOpacities();

                await Task.WhenAll(
                    contentColumn is not null ? FadeElementAsync(contentColumn, 0, 1) : Task.CompletedTask,
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
                var contentColumn = MiddleStepPage?.ContentColumnElement;
                await Task.WhenAll(
                    contentColumn is not null ? FadeElementAsync(contentColumn, 1, 0) : Task.CompletedTask,
                    oldStripe is not null ? FadeElementAsync(oldStripe, 1, 0) : Task.CompletedTask);

                var result = await changeStepAsync();
                var newStripe = GetStripeForStep(_viewModel.CurrentStepIndex);
                SyncStripeOpacities();

                await Task.WhenAll(
                    contentColumn is not null ? FadeElementAsync(contentColumn, 0, 1) : Task.CompletedTask,
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
        for (var i = 2; i <= 7; i++)
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
