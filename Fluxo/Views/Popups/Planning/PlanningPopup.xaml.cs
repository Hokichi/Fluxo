using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Fluxo.Services.Dialogs;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups.Planning;
using Fluxo.Views.CustomControls;

namespace Fluxo.Views.Popups.Planning;

public partial class PlanningPopup : BasePopup
{
    private const int IncomesStep = 0;
    private const int AllocationStep = 1;
    private const int ExpensesStep = 2;
    private const int StepCount = 3;

    private readonly IDialogService _dialogService;
    private readonly PlanningPopupVM _viewModel;
    private PlanningSnapshot? _completionSnapshot;
    private int _currentStep;
    private bool _skipDiscardPrompt;

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
            FluxoMessageBox.Show(this, $"Unable to import fixed expenses. {ex.Message}", "Planning",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            checkBox.IsEnabled = true;
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (_currentStep == IncomesStep)
            return;

        _currentStep--;
        UpdateStep();
    }

    private void OnNextOrFinishClick(object sender, RoutedEventArgs e)
    {
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

        if (_currentStep == ExpensesStep)
        {
            _completionSnapshot = _viewModel.BuildSnapshot();
            _skipDiscardPrompt = true;
            DialogResult = true;
            Close();
            return;
        }

        _currentStep++;
        UpdateStep();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlanningPopupVM.NeedsPercent)
            or nameof(PlanningPopupVM.WantsPercent)
            or nameof(PlanningPopupVM.InvestPercent)
            or nameof(PlanningPopupVM.IsAllocationValid))
        {
            UpdateAllocationSummary();
            UpdateStep();
        }
    }

    private void UpdateStep()
    {
        IncomesStepPanel.Visibility = _currentStep == IncomesStep ? Visibility.Visible : Visibility.Collapsed;
        AllocationStepPanel.Visibility = _currentStep == AllocationStep ? Visibility.Visible : Visibility.Collapsed;
        ExpensesStepPanel.Visibility = _currentStep == ExpensesStep ? Visibility.Visible : Visibility.Collapsed;

        BackButton.IsEnabled = _currentStep > IncomesStep;
        NextButton.Content = _currentStep == ExpensesStep ? "Finish" : "Next";
        NextButton.IsEnabled = _currentStep != AllocationStep || _viewModel.IsAllocationValid;
        StepCounterText.Text = $"Step {_currentStep + 1} of {StepCount}";

        SetStepBadge(IncomesStepBadge, IncomesStepText, _currentStep == IncomesStep);
        SetStepBadge(AllocationStepBadge, AllocationStepText, _currentStep == AllocationStep);
        SetStepBadge(ExpensesStepBadge, ExpensesStepText, _currentStep == ExpensesStep);
    }

    private void UpdateAllocationSummary()
    {
        var total = _viewModel.NeedsPercent + _viewModel.WantsPercent + _viewModel.InvestPercent;
        AllocationTotalText.Text = $"{total}%";

        if (_viewModel.IsAllocationValid)
        {
            AllocationValidationHint.Visibility = Visibility.Collapsed;
            AllocationValidationHint.Text = string.Empty;
            return;
        }

        AllocationValidationHint.Visibility = Visibility.Visible;
        AllocationValidationHint.Text = $"Current total is {total}%. Adjust the sliders until the allocation equals 100%.";
    }

    private void SetStepBadge(Border badge, TextBlock label, bool isActive)
    {
        badge.Background = GetBrush(isActive ? "Brush.Primary" : "Brush.Background.Surface");
        badge.BorderBrush = GetBrush(isActive ? "Brush.Primary.Hover" : "Brush.Border.Subtle");
        badge.BorderThickness = new Thickness(1);
        label.Foreground = isActive ? Brushes.White : GetBrush("Brush.Text.Secondary");
    }

    private Brush GetBrush(string resourceKey)
    {
        return TryFindResource(resourceKey) as Brush ?? Brushes.Transparent;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Closed -= OnClosed;
        _viewModel.Dispose();

        if (DialogResult == true && _completionSnapshot is { } snapshot)
            Dispatcher.BeginInvoke(() => _dialogService.ShowPlanningReport(snapshot, Owner));
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
