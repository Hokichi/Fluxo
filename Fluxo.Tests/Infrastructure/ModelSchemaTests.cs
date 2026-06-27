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

        var account = model.FindEntityType(typeof(Account))!;
        Assert.NotNull(account.FindProperty(nameof(Account.PinnedOnUI)));
        Assert.Null(account.FindProperty("ShowOnUI"));
        Assert.Equal("NUMERIC", account.FindProperty(nameof(Account.MaximumSpending))!.GetColumnType());
        Assert.False(account.FindProperty(nameof(Account.MaximumSpending))!.IsNullable);
        Assert.Equal("NUMERIC", account.FindProperty(nameof(Account.MinimumPayment))!.GetColumnType());
        Assert.True(account.FindProperty(nameof(Account.MinimumPayment))!.IsNullable);
        Assert.Equal(false, account.FindProperty(nameof(Account.IsDefault))!.GetDefaultValue());
        Assert.Contains(account.GetIndexes(), index =>
            index.IsUnique &&
            index.GetFilter() == "IsDefault = 1" &&
            index.Properties.Single().Name == nameof(Account.IsDefault));

        var tag = model.FindEntityType(typeof(Tag))!;
        Assert.Equal("Tags", tag.GetTableName());
        Assert.Equal("NUMERIC", tag.FindProperty(nameof(Tag.SpendingLimit))!.GetColumnType());
        Assert.True(tag.FindProperty(nameof(Tag.SpendingLimit))!.IsNullable);

        var expense = model.FindEntityType(typeof(Expense))!;
        Assert.False(expense.FindProperty(nameof(Expense.IsLend))!.IsNullable);
        Assert.Equal(false, expense.FindProperty(nameof(Expense.IsLend))!.GetDefaultValue());

        var expenseLog = model.FindEntityType(typeof(ExpenseLog))!;
        Assert.False(expenseLog.FindProperty(nameof(ExpenseLog.IsLend))!.IsNullable);
        Assert.Equal(false, expenseLog.FindProperty(nameof(ExpenseLog.IsLend))!.GetDefaultValue());
        Assert.False(expenseLog.FindProperty(nameof(ExpenseLog.IsPinned))!.IsNullable);
        Assert.Equal(false, expenseLog.FindProperty(nameof(ExpenseLog.IsPinned))!.GetDefaultValue());
        var parentLogId = expenseLog.FindProperty(nameof(ExpenseLog.ParentLogId));
        Assert.NotNull(parentLogId);
        Assert.True(parentLogId!.IsNullable);
        Assert.Contains(expenseLog.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Any(property => property.Name == nameof(ExpenseLog.ParentLogId)) &&
            foreignKey.PrincipalEntityType.ClrType == typeof(ExpenseLog) &&
            foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
        var parentLogNavigation = expenseLog.FindNavigation(nameof(ExpenseLog.ParentLog));
        Assert.NotNull(parentLogNavigation);
        Assert.False(parentLogNavigation!.IsEagerLoaded);
        Assert.True(expenseLog.FindNavigation(nameof(ExpenseLog.Expense))!.IsEagerLoaded);
        Assert.True(expenseLog.FindNavigation(nameof(ExpenseLog.Account))!.IsEagerLoaded);

        var incomeLog = model.FindEntityType(typeof(IncomeLog))!;
        Assert.False(incomeLog.FindProperty(nameof(IncomeLog.IsDebt))!.IsNullable);
        Assert.Equal(false, incomeLog.FindProperty(nameof(IncomeLog.IsDebt))!.GetDefaultValue());
        Assert.False(incomeLog.FindProperty(nameof(IncomeLog.IsForDeletion))!.IsNullable);
        Assert.False(incomeLog.FindProperty(nameof(IncomeLog.IsPinned))!.IsNullable);
        Assert.Equal(false, incomeLog.FindProperty(nameof(IncomeLog.IsPinned))!.GetDefaultValue());

        var transaction = model.FindEntityType(typeof(Transaction))!;
        Assert.Equal("Transactions", transaction.GetTableName());
        Assert.Equal(false, transaction.FindProperty(nameof(Transaction.IsIoU))!.GetDefaultValue());
        Assert.Equal(false, transaction.FindProperty(nameof(Transaction.IsExcludedFromBudget))!.GetDefaultValue());

        var savingGoal = model.FindEntityType(typeof(SavingGoal))!;
        Assert.True(savingGoal.FindProperty(nameof(SavingGoal.SavingEndDate))!.IsNullable);

        Assert.Null(savingGoal.FindProperty("RecurringPeriod"));

        var recurringTransaction = model.FindEntityType(typeof(RecurringTransaction))!;
        Assert.False(recurringTransaction.FindProperty("RecurringPeriod")!.IsNullable);
        Assert.False(recurringTransaction.FindProperty("RecurringTime")!.IsNullable);
        Assert.True(recurringTransaction.FindProperty(nameof(RecurringTransaction.Category))!.IsNullable);
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
        var source = new Account
        {
            MaximumSpending = 500m,
            MinimumPayment = 25m,
            PinnedOnUI = true,
            IsDefault = true
        };
        var sourceVm = new AccountVM
        {
            MaximumSpending = source.MaximumSpending,
            MinimumPayment = source.MinimumPayment,
            PinnedOnUI = source.PinnedOnUI,
            IsDefault = source.IsDefault
        };

        Assert.Equal(500m, sourceVm.MaximumSpending);
        Assert.Equal(25m, sourceVm.MinimumPayment);
        Assert.True(sourceVm.PinnedOnUI);
        Assert.True(sourceVm.IsDefault);

        var tag = new Tag { SpendingLimit = 250m };
        var tagVm = new TagVM { SpendingLimit = tag.SpendingLimit };
        Assert.Equal(250m, tagVm.SpendingLimit);

        var expense = new Expense { IsLend = true };
        var expenseDto = new ExpenseDto { IsLend = expense.IsLend };
        var expenseVm = new ExpenseVM { IsLend = expenseDto.IsLend };
        Assert.True(expenseDto.IsLend);
        Assert.True(expenseVm.IsLend);

        var expenseLog = new ExpenseLog { IsPinned = true, ParentLogId = 10, IsLend = true };
        var expenseLogDto = new ExpenseLogDto
        {
            IsPinned = expenseLog.IsPinned,
            ParentLogId = expenseLog.ParentLogId,
            IsLend = expenseLog.IsLend
        };
        var expenseLogVm = new ExpenseLogVM
        {
            IsPinned = expenseLog.IsPinned,
            ParentLogId = expenseLogDto.ParentLogId,
            IsLend = expenseLogDto.IsLend
        };
        Assert.True(expenseLogDto.IsPinned);
        Assert.True(expenseLogVm.IsPinned);
        Assert.True(expenseLogDto.IsLend);
        Assert.True(expenseLogVm.IsLend);
        Assert.Equal(10, expenseLogDto.ParentLogId);
        Assert.Equal(10, expenseLogVm.ParentLogId);

        var incomeLog = new IncomeLog { IsForDeletion = true, IsPinned = true, IsDebt = true };
        var incomeLogDto = new IncomeLogDto
        {
            IsForDeletion = incomeLog.IsForDeletion,
            IsPinned = incomeLog.IsPinned,
            IsDebt = incomeLog.IsDebt
        };
        var incomeLogVm = new IncomeLogVM
        {
            IsPinned = incomeLog.IsPinned,
            IsDebt = incomeLogDto.IsDebt
        };
        Assert.True(incomeLogDto.IsForDeletion);
        Assert.True(incomeLogDto.IsPinned);
        Assert.True(incomeLogVm.IsPinned);
        Assert.True(incomeLogDto.IsDebt);
        Assert.True(incomeLogVm.IsDebt);

        var goal = new SavingGoal { SavingEndDate = null };
        var goalVm = new SavingGoalVM
        {
            SavingEndDate = goal.SavingEndDate
        };

        Assert.Null(goalVm.SavingEndDate);

        var recurringTransaction = new RecurringTransaction
        {
            RecurringPeriod = RecurringPeriod.Biweekly,
            RecurringTime = 5,
            Category = ExpenseCategory.Wants
        };
        var recurringTransactionDto = new RecurringTransactionDto
        {
            Category = recurringTransaction.Category
        };
        var recurringTransactionVm = new RecurringTransactionVM
        {
            RecurringPeriod = recurringTransaction.RecurringPeriod,
            RecurringTime = recurringTransaction.RecurringTime,
            Category = recurringTransaction.Category
        };

        Assert.Null(typeof(SavingGoal).GetProperty("RecurringPeriod"));
        Assert.Null(typeof(SavingGoalVM).GetProperty("RecurringPeriod"));
        Assert.Equal(ExpenseCategory.Wants, recurringTransactionDto.Category);
        Assert.Equal(RecurringPeriod.Biweekly, recurringTransactionVm.RecurringPeriod);
        Assert.Equal(5, recurringTransactionVm.RecurringTime);
        Assert.Equal(ExpenseCategory.Wants, recurringTransactionVm.Category);
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
