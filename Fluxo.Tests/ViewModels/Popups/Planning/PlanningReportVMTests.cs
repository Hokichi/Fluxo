using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups.Planning;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups.Planning;

public class PlanningReportVMTests
{
    [Fact]
    public void BuildSnapshot_CreatesDeepCopiesForReportHandoff()
    {
        var popup = CreatePopupVm();
        var income = CreateIncome(1, 125m);
        var expense = CreateExpense(2, "Rent");
        popup.Incomes.Add(income);
        popup.Expenses.Add(expense);
        popup.NeedsPercent = 45;
        popup.WantsPercent = 35;
        popup.InvestPercent = 20;

        var snapshot = popup.BuildSnapshot();

        income.Amount = 999m;
        income.Notes = "Mutated income";
        income.Account.Name = "Mutated income source";
        expense.Amount = 888m;
        expense.Name = "Mutated expense";
        expense.ExpenseTag.Name = "Mutated tag";
        expense.Account.Name = "Mutated expense source";
        popup.Incomes.Clear();
        popup.Expenses.Clear();

        var report = new PlanningReportVM(snapshot);

        Assert.Single(report.Incomes);
        Assert.Single(report.Expenses);
        Assert.Equal(125m, report.Incomes[0].Amount);
        Assert.Equal("Income 1", report.Incomes[0].Notes);
        Assert.Equal("Income Source 1", report.Incomes[0].Account.Name);
        Assert.Equal(200m, report.Expenses[0].Amount);
        Assert.Equal("Rent", report.Expenses[0].Name);
        Assert.Equal("Tag 2", report.Expenses[0].ExpenseTag.Name);
        Assert.Equal("Expense Source 2", report.Expenses[0].Account.Name);
        Assert.NotSame(income, report.Incomes[0]);
        Assert.NotSame(income.Account, report.Incomes[0].Account);
        Assert.NotSame(expense, report.Expenses[0]);
        Assert.NotSame(expense.ExpenseTag, report.Expenses[0].ExpenseTag);
        Assert.NotSame(expense.Account, report.Expenses[0].Account);
        Assert.Equal(45, report.NeedsPercent);
        Assert.Equal(35, report.WantsPercent);
        Assert.Equal(20, report.InvestPercent);
        Assert.Equal(125m, report.TotalIncome);
        Assert.Equal(200m, report.TotalExpenses);
        Assert.Equal(-75m, report.Balance);
        Assert.Equal(1d, report.NeedsUsage, 5);
        Assert.Equal(2.55556d, report.NeedsOverflow, 5);
        Assert.Equal(356, report.NeedsUsagePercent);
        Assert.Equal(0d, report.WantsUsage, 5);
        Assert.Equal(0d, report.WantsOverflow, 5);
        Assert.Equal(0d, report.InvestUsage, 5);
        Assert.Equal(0d, report.InvestOverflow, 5);
    }

    [Fact]
    public void PlanningReportVM_ClonesSnapshotAndSupportsEditableIncomeAndExpenseRows()
    {
        var sourceIncome = CreateIncome(1, 125m);
        var sourceExpense = CreateExpense(2, "Rent");
        var snapshot = new PlanningSnapshot(
            [sourceIncome],
            [sourceExpense],
            needsPercent: 50,
            wantsPercent: 30,
            investPercent: 20);

        var report = new PlanningReportVM(snapshot);

        sourceIncome.Amount = 999m;
        sourceExpense.Name = "Mutated source";
        report.Incomes[0].Amount = 150m;
        report.Expenses[0].Name = "Edited rent";
        var addedIncome = CreateIncome(3, 300m);
        var addedExpense = CreateExpense(4, "Groceries");
        report.AddIncome(addedIncome);
        report.AddExpense(addedExpense);

        Assert.Equal(2, report.Incomes.Count);
        Assert.Equal(2, report.Expenses.Count);
        Assert.Equal(150m, report.Incomes[0].Amount);
        Assert.Equal("Edited rent", report.Expenses[0].Name);
        Assert.Equal(300m, report.Incomes[1].Amount);
        Assert.Equal("Groceries", report.Expenses[1].Name);
        Assert.Equal(50, report.NeedsPercent);
        Assert.Equal(30, report.WantsPercent);
        Assert.Equal(20, report.InvestPercent);
        Assert.NotSame(addedIncome, report.Incomes[1]);
        Assert.NotSame(addedExpense, report.Expenses[1]);
        Assert.Equal(450m, report.TotalIncome);
        Assert.Equal(600m, report.TotalExpenses);
        Assert.Equal(-150m, report.Balance);

        Assert.True(report.RemoveIncome(addedIncome));
        Assert.True(report.RemoveExpense(addedExpense));

        Assert.Single(report.Incomes);
        Assert.Single(report.Expenses);
        Assert.Equal(150m, report.Incomes[0].Amount);
        Assert.Equal("Edited rent", report.Expenses[0].Name);
        Assert.Equal(150m, report.TotalIncome);
        Assert.Equal(200m, report.TotalExpenses);
        Assert.Equal(-50m, report.Balance);
    }

    [Fact]
    public void PlanningReportVM_ComputesAllocationUsageAndOverflowPerCategory()
    {
        var snapshot = new PlanningSnapshot(
            [CreateIncome(1, 1000m)],
            [
                CreateExpense(1, "Rent", ExpenseCategory.Needs, 600m),
                CreateExpense(2, "Coffee", ExpenseCategory.Wants, 250m),
                CreateExpense(3, "ETF", ExpenseCategory.Savings, 300m)
            ],
            needsPercent: 50,
            wantsPercent: 30,
            investPercent: 20);

        var report = new PlanningReportVM(snapshot);

        Assert.Equal(1d, report.NeedsUsage, 5);
        Assert.Equal(0.2d, report.NeedsOverflow, 5);
        Assert.Equal(120, report.NeedsUsagePercent);
        Assert.Equal(0.83333d, report.WantsUsage, 5);
        Assert.Equal(0d, report.WantsOverflow, 5);
        Assert.Equal(83, report.WantsUsagePercent);
        Assert.Equal(1d, report.InvestUsage, 5);
        Assert.Equal(0.5d, report.InvestOverflow, 5);
        Assert.Equal(150, report.InvestUsagePercent);

        report.Expenses[1].Amount = 360m;

        Assert.Equal(1d, report.WantsUsage, 5);
        Assert.Equal(0.2d, report.WantsOverflow, 5);
        Assert.Equal(120, report.WantsUsagePercent);

        report.Expenses[0].ExpenseCategory = ExpenseCategory.Wants;

        Assert.Equal(0d, report.NeedsUsage, 5);
        Assert.Equal(0d, report.NeedsOverflow, 5);
        Assert.Equal(0, report.NeedsUsagePercent);
        Assert.Equal(1d, report.WantsUsage, 5);
        Assert.Equal(2.2d, report.WantsOverflow, 5);
        Assert.Equal(320, report.WantsUsagePercent);
    }

    private static PlanningPopupVM CreatePopupVm()
    {
        var appData = Substitute.For<IAppDataService>();
        appData.GetExpensesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Fluxo.Core.Entities.Expense>>([]));
        return new PlanningPopupVM(appData);
    }

    private static IncomeLogVM CreateIncome(int id, decimal amount)
    {
        return new IncomeLogVM
        {
            Id = id,
            Amount = amount,
            AddedOn = DateTime.UnixEpoch.AddDays(id),
            Notes = $"Income {id}",
            Account = new AccountVM
            {
                Id = id,
                Name = $"Income Source {id}",
                AccountType = AccountType.Checking,
                Balance = 500m,
                IsEnabled = true,
                PinnedOnUI = true
            }
        };
    }

    private static ExpenseVM CreateExpense(
        int id,
        string name,
        ExpenseCategory category = ExpenseCategory.Needs,
        decimal? amount = null)
    {
        return new ExpenseVM
        {
            Id = id,
            Name = name,
            Amount = amount ?? id * 100m,
            ExpenseCategory = category,
            ExpenseTag = new ExpenseTagVM
            {
                Id = id,
                Name = $"Tag {id}",
                HexCode = "#000000",
                IsSystemTag = false
            },
            Account = new AccountVM
            {
                Id = id,
                Name = $"Expense Source {id}",
                AccountType = AccountType.Checking,
                Balance = 1000m,
                IsEnabled = true,
                PinnedOnUI = true
            }
        };
    }
}



