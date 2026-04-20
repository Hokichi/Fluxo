using AutoMapper;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Services.Persistence;
using Fluxo.Tests.TestDoubles;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.Services.Persistence;

public sealed class ExpenseLogServiceTests
{
    [Fact]
    public async Task DeleteAsync_RestoresBalanceForCheckingSource_AndMarksLogForDeletion()
    {
        var (sut, unitOfWork, expenseLogs, spendingSources) = CreateSut();
        var expenseLog = new ExpenseLog
        {
            Id = 44,
            SpendingSourceId = 7,
            Amount = 35m,
            IsForDeletion = false
        };
        var source = new SpendingSource
        {
            Id = 7,
            SpendingSourceType = SpendingSourceType.Checking,
            Balance = 100m,
            SpentAmount = 12m
        };
        expenseLogs.GetByLogIdAsync(expenseLog.Id, Arg.Any<CancellationToken>())
            .Returns(expenseLog);
        spendingSources.GetByIdAsync(source.Id, Arg.Any<CancellationToken>())
            .Returns(source);

        await sut.DeleteAsync(expenseLog.Id);

        Assert.True(expenseLog.IsForDeletion);
        Assert.Equal(135m, source.Balance);
        Assert.Equal(12m, source.SpentAmount);
        spendingSources.Received(1).Update(source);
        expenseLogs.Received(1).Update(expenseLog);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_RestoresSpentAmountForCreditSource_AndMarksLogForDeletion()
    {
        var (sut, unitOfWork, expenseLogs, spendingSources) = CreateSut();
        var expenseLog = new ExpenseLog
        {
            Id = 99,
            SpendingSourceId = 2,
            Amount = 30m,
            IsForDeletion = false
        };
        var source = new SpendingSource
        {
            Id = 2,
            SpendingSourceType = SpendingSourceType.Credit,
            Balance = 500m,
            SpentAmount = 80m
        };
        expenseLogs.GetByLogIdAsync(expenseLog.Id, Arg.Any<CancellationToken>())
            .Returns(expenseLog);
        spendingSources.GetByIdAsync(source.Id, Arg.Any<CancellationToken>())
            .Returns(source);

        await sut.DeleteAsync(expenseLog.Id);

        Assert.True(expenseLog.IsForDeletion);
        Assert.Equal(500m, source.Balance);
        Assert.Equal(50m, source.SpentAmount);
        spendingSources.Received(1).Update(source);
        expenseLogs.Received(1).Update(expenseLog);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static (ExpenseLogService Sut, IUnitOfWork UnitOfWork, IExpenseLogRepository ExpenseLogs,
        ISpendingSourceRepository SpendingSources) CreateSut()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var expenseLogs = Substitute.For<IExpenseLogRepository>();
        var spendingSources = Substitute.For<ISpendingSourceRepository>();

        unitOfWork.ExpenseLogs.Returns(expenseLogs);
        unitOfWork.SpendingSources.Returns(spendingSources);

        var sut = new ExpenseLogService(new InlineDataOperationRunner(unitOfWork), Substitute.For<IMapper>());
        return (sut, unitOfWork, expenseLogs, spendingSources);
    }
}
