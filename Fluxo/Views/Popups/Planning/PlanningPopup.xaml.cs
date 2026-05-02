using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Fluxo.Core.Enums;
using Fluxo.Services.Dialogs;
using Fluxo.Services.Logging;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups.Planning;
using Fluxo.Views.CustomControls;

namespace Fluxo.Views.Popups.Planning;

public partial class PlanningPopup : BasePopup
{
    private const int IncomesStep = 0;
    private const int ExpensesStep = 1;
    private const int AllocationStep = 2;
    private const int StepCount = 3;
    private static readonly Duration StepFadeDuration = new(TimeSpan.FromMilliseconds(150));

    private readonly IDialogService _dialogService;
    private readonly PlanningPopupVM _viewModel;
    private PlanningSnapshot? _completionSnapshot;
    private int _currentStep;
    private bool _skipDiscardPrompt;
    private bool _isStepTransitioning;

    public PlanningPopup(PlanningPopupVM viewModel, IDialogService dialogService)
    {
        AllocationCategories = CreateAllocationCategories();
        InitializeComponent();

        _dialogService = dialogService;
        _viewModel = viewModel;
        DataContext = viewModel;

        if (_viewModel.Incomes.Count == 0)
            _viewModel.Incomes.Add(new IncomeLogVM { AddedOn = DateTime.Now });

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += OnClosed;

        ApplyStepPanelState();
        UpdateStep();
        UpdateAllocationSummary();
    }

    public IReadOnlyList<AllocationCategoryOption> AllocationCategories { get; }

    private static IReadOnlyList<AllocationCategoryOption> CreateAllocationCategories()
    {
        return
        [
            new AllocationCategoryOption(ExpenseCategory.Needs, "Needs"),
            new AllocationCategoryOption(ExpenseCategory.Wants, "Wants"),
            new AllocationCategoryOption(ExpenseCategory.Savings, "Invest")
        ];
    }

    private void OnAddIncomeClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Incomes.Add(new IncomeLogVM { AddedOn = DateTime.Now });
    }

    private void OnRemoveIncomeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: IncomeLogVM income })
            _viewModel.Incomes.Remove(income);
    }

    private void OnAddExpenseClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Expenses.Add(new ExpenseVM
        {
            ExpenseCategory = ExpenseCategory.Needs,
            ExpenseKind = ExpenseKind.Manual,
            IsActive = true
        });
    }

    private void OnRemoveExpenseClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ExpenseVM expense })
            _viewModel.Expenses.Remove(expense);
    }

    private async void OnImportFixedExpensesChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox)
            return;

        checkBox.IsEnabled = false;

        try
        {
            await _viewModel.SetImportFixedExpensesAsync(checkBox.IsChecked == true);
        }
        catch (Exception ex)
        {
            FluxoLogManager.LogError(ex, "Unable to import fixed expenses in planning popup.");
            FluxoMessageBox.Show(this, FluxoLogManager.CreateFailureMessage("import fixed expenses"), "Planning",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            checkBox.IsEnabled = true;
        }
    }

    private async void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (_currentStep == IncomesStep || _isStepTransitioning)
            return;

        await TransitionToStepAsync(_currentStep - 1);
    }

    private async void OnNextOrFinishClick(object sender, RoutedEventArgs e)
    {
        if (_isStepTransitioning)
            return;

        if (_currentStep == IncomesStep && _viewModel.ShouldPromptCloseOnMissingIncome())
        {
            var confirmation = FluxoMessageBox.Show(this,
                "No income has been added to this plan. Close Planning instead?",
                "Planning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation == MessageBoxResult.Yes)
            {
                _skipDiscardPrompt = true;
                DialogResult = false;
                Close();
            }

            return;
        }

        if (_currentStep == AllocationStep && !_viewModel.IsAllocationValid)
            return;

        if (_currentStep == AllocationStep)
        {
            _completionSnapshot = _viewModel.BuildSnapshot();
            _skipDiscardPrompt = true;
            DialogResult = true;
            CloseForPopupHandoff();
            return;
        }

        await TransitionToStepAsync(_currentStep + 1);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlanningPopupVM.TotalIncome))
            UpdateAddIncomeButtonState();

        if (e.PropertyName is nameof(PlanningPopupVM.NeedsPercent)
            or nameof(PlanningPopupVM.WantsPercent)
            or nameof(PlanningPopupVM.InvestPercent)
            or nameof(PlanningPopupVM.IsAllocationValid))
        {
            UpdateAllocationSummary();
            UpdateStep();
        }
    }

    private async Task TransitionToStepAsync(int targetStep)
    {
        if (targetStep < IncomesStep || targetStep > AllocationStep || targetStep == _currentStep)
            return;

        var currentPanel = GetPanelForStep(_currentStep);
        var nextPanel = GetPanelForStep(targetStep);

        if (currentPanel is null || nextPanel is null)
        {
            _currentStep = targetStep;
            ApplyStepPanelState();
            UpdateStep();
            return;
        }

        _isStepTransitioning = true;
        UpdateStep();

        nextPanel.Visibility = Visibility.Visible;
        nextPanel.Opacity = 0;

        _currentStep = targetStep;
        UpdateStep();

        await Task.WhenAll(
            FadeElementAsync(currentPanel, 1, 0),
            FadeElementAsync(nextPanel, 0, 1));

        currentPanel.Visibility = Visibility.Collapsed;
        currentPanel.Opacity = 0;

        _isStepTransitioning = false;
        UpdateStep();
    }

    private static Task FadeElementAsync(UIElement element, double from, double to)
    {
        var tcs = new TaskCompletionSource<bool>();

        var animation = new DoubleAnimation(from, to, StepFadeDuration)
        {
            EasingFunction = new CubicEase { EasingMode = to < from ? EasingMode.EaseIn : EasingMode.EaseOut }
        };

        animation.Completed += (_, _) => tcs.TrySetResult(true);
        element.BeginAnimation(OpacityProperty, animation);

        return tcs.Task;
    }

    private void ApplyStepPanelState()
    {
        IncomesStepPanel.Visibility = _currentStep == IncomesStep ? Visibility.Visible : Visibility.Collapsed;
        AllocationStepPanel.Visibility = _currentStep == AllocationStep ? Visibility.Visible : Visibility.Collapsed;
        ExpensesStepPanel.Visibility = _currentStep == ExpensesStep ? Visibility.Visible : Visibility.Collapsed;

        IncomesStepPanel.Opacity = _currentStep == IncomesStep ? 1 : 0;
        AllocationStepPanel.Opacity = _currentStep == AllocationStep ? 1 : 0;
        ExpensesStepPanel.Opacity = _currentStep == ExpensesStep ? 1 : 0;
    }

    private FrameworkElement? GetPanelForStep(int step) => step switch
    {
        IncomesStep => IncomesStepPanel,
        AllocationStep => AllocationStepPanel,
        ExpensesStep => ExpensesStepPanel,
        _ => null
    };

    private void UpdateStep()
    {
        BackButton.IsEnabled = !_isStepTransitioning && _currentStep > IncomesStep;
        BackButton.Visibility = _currentStep == IncomesStep ? Visibility.Collapsed : Visibility.Visible;
        NextButton.IsEnabled = !_isStepTransitioning && (_currentStep != AllocationStep || _viewModel.IsAllocationValid);
        NextButton.ButtonIcon = FindResource(_currentStep == AllocationStep
            ? "Check"
            : "AngleRight");
        NextButton.ToolTip = _currentStep == AllocationStep ? "Finish" : "Next";

        UpdateAddIncomeButtonState();

        StepNavigator.StepCount = StepCount;
        StepNavigator.CurrentStep = _currentStep + 1;
    }

    private void UpdateAddIncomeButtonState()
    {
        var lastIncome = _viewModel.Incomes.LastOrDefault();
        AddIncomeButton.IsEnabled = !_isStepTransitioning && (lastIncome is null || lastIncome.Amount != 0m);
    }

    private void UpdateAllocationSummary()
    {
        var total = _viewModel.NeedsPercent + _viewModel.WantsPercent + _viewModel.InvestPercent;
        AllocationTotalText.Text = total + "%";

        if (_viewModel.IsAllocationValid)
        {
            AllocationValidationHint.Visibility = Visibility.Collapsed;
            AllocationValidationHint.Text = string.Empty;
            return;
        }

        AllocationValidationHint.Visibility = Visibility.Visible;
        AllocationValidationHint.Text = "Current total is " + total + "%. Adjust the sliders until the allocation equals 100%.";
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Closed -= OnClosed;
        _viewModel.Dispose();

        if (DialogResult == true && _completionSnapshot is { } snapshot)
            Dispatcher.BeginInvoke(() => _dialogService.ShowPlanningReport(snapshot.WithoutZeroAmountEntries(), Owner));
    }

    private void OnPopupPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None || _isStepTransitioning)
            return;

        e.Handled = true;
        OnNextOrFinishClick(NextButton, new RoutedEventArgs(Button.ClickEvent, NextButton));
    }

    protected override void OnCloseButtonClick()
    {
        if (!_skipDiscardPrompt && _viewModel.HasAnyInput)
        {
            var result = FluxoMessageBox.Show(this,
                "Discard this planning session?",
                "Planning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;
        }

        base.OnCloseButtonClick();
    }

    public sealed class AllocationCategoryOption(ExpenseCategory value, string label)
    {
        public ExpenseCategory Value { get; } = value;
        public string Label { get; } = label;
    }
}
