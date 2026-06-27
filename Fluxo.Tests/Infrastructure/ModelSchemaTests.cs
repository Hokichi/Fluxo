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

        var transaction = model.FindEntityType(typeof(Transaction))!;
        Assert.Equal("Transactions", transaction.GetTableName());
        Assert.Equal(false, transaction.FindProperty(nameof(Transaction.IsIoU))!.GetDefaultValue());
        Assert.Equal(false, transaction.FindProperty(nameof(Transaction.IsExcludedFromBudget))!.GetDefaultValue());
        Assert.True(transaction.FindProperty(nameof(Transaction.ParentTransactionId))!.IsNullable);
        Assert.Contains(transaction.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Any(property => property.Name == nameof(Transaction.ParentTransactionId)) &&
            foreignKey.PrincipalEntityType.ClrType == typeof(Transaction) &&
            foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Null(model.FindEntityType(typeof(Expense)));
        Assert.Null(model.FindEntityType(typeof(ExpenseLog)));
        Assert.Null(model.FindEntityType(typeof(IncomeLog)));

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

        var expense = new Expense { IsIoU = true };
        var expenseDto = new ExpenseDto { IsIoU = expense.IsIoU };
        var expenseVm = new ExpenseVM { IsIoU = expenseDto.IsIoU };
        Assert.True(expenseDto.IsIoU);
        Assert.True(expenseVm.IsIoU);

        var expenseLog = new ExpenseLog { IsPinned = true, ParentLogId = 10, IsIoU = true };
        var expenseLogDto = new ExpenseLogDto
        {
            IsPinned = expenseLog.IsPinned,
            ParentLogId = expenseLog.ParentLogId,
            IsIoU = expenseLog.IsIoU
        };
        var expenseLogVm = new ExpenseLogVM
        {
            IsPinned = expenseLog.IsPinned,
            ParentLogId = expenseLogDto.ParentLogId,
            IsIoU = expenseLogDto.IsIoU
        };
        Assert.True(expenseLogDto.IsPinned);
        Assert.True(expenseLogVm.IsPinned);
        Assert.True(expenseLogDto.IsIoU);
        Assert.True(expenseLogVm.IsIoU);
        Assert.Equal(10, expenseLogDto.ParentLogId);
        Assert.Equal(10, expenseLogVm.ParentLogId);

        var incomeLog = new IncomeLog { IsForDeletion = true, IsPinned = true, IsIoU = true };
        var incomeLogDto = new IncomeLogDto
        {
            IsForDeletion = incomeLog.IsForDeletion,
            IsPinned = incomeLog.IsPinned,
            IsIoU = incomeLog.IsIoU
        };
        var incomeLogVm = new IncomeLogVM
        {
            IsPinned = incomeLog.IsPinned,
            IsIoU = incomeLogDto.IsIoU
        };
        Assert.True(incomeLogDto.IsForDeletion);
        Assert.True(incomeLogDto.IsPinned);
        Assert.True(incomeLogVm.IsPinned);
        Assert.True(incomeLogDto.IsIoU);
        Assert.True(incomeLogVm.IsIoU);

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
