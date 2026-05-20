using Fluxo.Core.Constants;
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
        Assert.Equal("NUMERIC", spendingSource.FindProperty(nameof(SpendingSource.MaximumSpending))!.GetColumnType());
        Assert.False(spendingSource.FindProperty(nameof(SpendingSource.MaximumSpending))!.IsNullable);
        Assert.Equal("NUMERIC", spendingSource.FindProperty(nameof(SpendingSource.MinimumPayment))!.GetColumnType());
        Assert.True(spendingSource.FindProperty(nameof(SpendingSource.MinimumPayment))!.IsNullable);

        var savingGoal = model.FindEntityType(typeof(SavingGoal))!;
        Assert.True(savingGoal.FindProperty(nameof(SavingGoal.SavingEndDate))!.IsNullable);

        Assert.Null(savingGoal.FindProperty("RecurringPeriod"));

        var recurringTransaction = model.FindEntityType(typeof(RecurringTransaction))!;
        Assert.False(recurringTransaction.FindProperty("RecurringPeriod")!.IsNullable);
        Assert.False(recurringTransaction.FindProperty("RecurringTime")!.IsNullable);
        Assert.Null(recurringTransaction.FindProperty("RecurringDate"));
    }

    [Fact]
    public void EntitiesAndViewModels_ExposeRequestedProperties()
    {
        var source = new SpendingSource
        {
            MaximumSpending = 500m,
            MinimumPayment = 25m
        };
        var sourceVm = new SpendingSourceVM
        {
            MaximumSpending = source.MaximumSpending,
            MinimumPayment = source.MinimumPayment
        };

        Assert.Equal(500m, sourceVm.MaximumSpending);
        Assert.Equal(25m, sourceVm.MinimumPayment);

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
        Assert.Equal(nameof(UserSettingNames.AllocationPeriod), UserSettingNames.AllocationPeriod);
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
    }

    private static FluxoDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FluxoDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        return new FluxoDbContext(options);
    }
}
