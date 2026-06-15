using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Data.Context;
using Fluxo.ViewModels.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fluxo.Tests.Infrastructure;

public sealed class ModelSchemaTests
{
    [Fact]
    public void FluxoDbContext_ModelIncludesRequestedSchemaChanges()
    {
        using var dbContext = CreateDbContext();
        var model = dbContext.Model;

        var spendingSource = model.FindEntityType(typeof(SpendingSource))!;
        Assert.NotNull(spendingSource.FindProperty(nameof(SpendingSource.PinnedOnUI)));
        Assert.Null(spendingSource.FindProperty("ShowOnUI"));
        Assert.Equal("NUMERIC", spendingSource.FindProperty(nameof(SpendingSource.MaximumSpending))!.GetColumnType());
        Assert.False(spendingSource.FindProperty(nameof(SpendingSource.MaximumSpending))!.IsNullable);
        Assert.Equal("NUMERIC", spendingSource.FindProperty(nameof(SpendingSource.MinimumPayment))!.GetColumnType());
        Assert.True(spendingSource.FindProperty(nameof(SpendingSource.MinimumPayment))!.IsNullable);

        var expenseTag = model.FindEntityType(typeof(ExpenseTag))!;
        Assert.Equal("NUMERIC", expenseTag.FindProperty(nameof(ExpenseTag.SpendingLimit))!.GetColumnType());
        Assert.True(expenseTag.FindProperty(nameof(ExpenseTag.SpendingLimit))!.IsNullable);

        var incomeLog = model.FindEntityType(typeof(IncomeLog))!;
        Assert.False(incomeLog.FindProperty(nameof(IncomeLog.IsForDeletion))!.IsNullable);

        var savingGoal = model.FindEntityType(typeof(SavingGoal))!;
        Assert.True(savingGoal.FindProperty(nameof(SavingGoal.SavingEndDate))!.IsNullable);

        Assert.Null(savingGoal.FindProperty("RecurringPeriod"));

        var recurringTransaction = model.FindEntityType(typeof(RecurringTransaction))!;
        Assert.False(recurringTransaction.FindProperty("RecurringPeriod")!.IsNullable);
        Assert.False(recurringTransaction.FindProperty("RecurringTime")!.IsNullable);
        Assert.Null(recurringTransaction.FindProperty("RecurringDate"));

        var budgetAllocation = model.FindEntityType(typeof(BudgetAllocation))!;
        Assert.Equal("BudgetAllocation", budgetAllocation.GetTableName());
        Assert.False(budgetAllocation.FindProperty(nameof(BudgetAllocation.NeedsThreshold))!.IsNullable);
        Assert.False(budgetAllocation.FindProperty(nameof(BudgetAllocation.WantsThreshold))!.IsNullable);
        Assert.False(budgetAllocation.FindProperty(nameof(BudgetAllocation.InvestThreshold))!.IsNullable);
        Assert.False(budgetAllocation.FindProperty(nameof(BudgetAllocation.AllocationPeriod))!.IsNullable);
        Assert.False(budgetAllocation.FindProperty(nameof(BudgetAllocation.PeriodStart))!.IsNullable);
        Assert.False(budgetAllocation.FindProperty(nameof(BudgetAllocation.CurrentPeriodIndex))!.IsNullable);
        Assert.False(budgetAllocation.FindProperty(nameof(BudgetAllocation.LastRolloverPeriodStart))!.IsNullable);
        Assert.False(budgetAllocation.FindProperty(nameof(BudgetAllocation.RolloverPolicy))!.IsNullable);
        Assert.False(budgetAllocation.FindProperty(nameof(BudgetAllocation.OverspendPolicy))!.IsNullable);
        Assert.Equal("NUMERIC", budgetAllocation.FindProperty(nameof(BudgetAllocation.AllocationLimit))!.GetColumnType());
        Assert.Equal("NUMERIC", budgetAllocation.FindProperty(nameof(BudgetAllocation.NeedsDebt))!.GetColumnType());
        Assert.Equal("NUMERIC", budgetAllocation.FindProperty(nameof(BudgetAllocation.WantsDebt))!.GetColumnType());
        Assert.Equal("NUMERIC", budgetAllocation.FindProperty(nameof(BudgetAllocation.InvestDebt))!.GetColumnType());

        var singletonKey = budgetAllocation.FindProperty("SingletonKey")!;
        Assert.False(singletonKey.IsNullable);
        Assert.Equal(1, singletonKey.GetDefaultValue());
        Assert.Contains(budgetAllocation.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Count == 1 &&
            string.Equals(index.Properties[0].Name, "SingletonKey", StringComparison.Ordinal));
    }

    [Fact]
    public void EntitiesAndViewModels_ExposeRequestedProperties()
    {
        var source = new SpendingSource
        {
            MaximumSpending = 500m,
            MinimumPayment = 25m,
            PinnedOnUI = true
        };
        var sourceVm = new SpendingSourceVM
        {
            MaximumSpending = source.MaximumSpending,
            MinimumPayment = source.MinimumPayment,
            PinnedOnUI = source.PinnedOnUI
        };

        Assert.Equal(500m, sourceVm.MaximumSpending);
        Assert.Equal(25m, sourceVm.MinimumPayment);
        Assert.True(sourceVm.PinnedOnUI);

        var tag = new ExpenseTag { SpendingLimit = 250m };
        var tagVm = new ExpenseTagVM { SpendingLimit = tag.SpendingLimit };
        Assert.Equal(250m, tagVm.SpendingLimit);

        var incomeLog = new IncomeLog { IsForDeletion = true };
        var incomeLogDto = new IncomeLogDto { IsForDeletion = incomeLog.IsForDeletion };
        Assert.True(incomeLogDto.IsForDeletion);

        var goal = new SavingGoal { SavingEndDate = null };
        var goalVm = new SavingGoalVM
        {
            SavingEndDate = goal.SavingEndDate
        };

        Assert.Null(goalVm.SavingEndDate);

        var recurringTransaction = new RecurringTransaction
        {
            RecurringPeriod = RecurringPeriod.Biweekly,
            RecurringTime = 5
        };
        var recurringTransactionVm = new RecurringTransactionVM
        {
            RecurringPeriod = recurringTransaction.RecurringPeriod,
            RecurringTime = recurringTransaction.RecurringTime
        };

        Assert.Null(typeof(SavingGoal).GetProperty("RecurringPeriod"));
        Assert.Null(typeof(SavingGoalVM).GetProperty("RecurringPeriod"));
        Assert.Equal(RecurringPeriod.Biweekly, recurringTransactionVm.RecurringPeriod);
        Assert.Equal(5, recurringTransactionVm.RecurringTime);
    }

    [Fact]
    public void PeriodEnums_ContainAllowedValues()
    {
        Assert.Equal(
            ["Weekly", "Biweekly", "Monthly", "Quarterly", "Yearly"],
            Enum.GetNames<AllocationPeriod>());

        Assert.Equal(
            ["None", "Weekly", "Biweekly", "Monthly"],
            Enum.GetNames<RecurringPeriod>());

        Assert.Equal(
            ["None", "Matching", "Pooled"],
            Enum.GetNames<RolloverPolicy>());

        Assert.Equal(
            ["Ignore", "SoftDebt", "HardStop"],
            Enum.GetNames<OverspendPolicy>());
    }

    private static FluxoDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FluxoDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        return new FluxoDbContext(options);
    }
}
