using System.Windows;
using System.Windows.Controls;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups.Planning;
using Fluxo.Views.CustomControls;

namespace Fluxo.Views.Popups.Planning;

public partial class PlanningReportPopup : BasePopup
{
    private readonly PlanningReportVM _viewModel;

    public PlanningReportPopup(PlanningReportVM viewModel)
    {
        AllocationCategories = CreateAllocationCategories();
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
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
        _viewModel.AddIncome(new IncomeLogVM { AddedOn = DateTime.Now });
    }

    private void OnRemoveIncomeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: IncomeLogVM income })
            _viewModel.RemoveIncome(income);
    }

    private void OnAddExpenseClick(object sender, RoutedEventArgs e)
    {
        _viewModel.AddExpense(new ExpenseVM
        {
            ExpenseCategory = ExpenseCategory.Needs,
            ExpenseKind = ExpenseKind.Manual,
            IsActive = true
        });
    }

    private void OnRemoveExpenseClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ExpenseVM expense })
            _viewModel.RemoveExpense(expense);
    }

    public sealed class AllocationCategoryOption(ExpenseCategory value, string label)
    {
        public ExpenseCategory Value { get; } = value;
        public string Label { get; } = label;
    }
}
