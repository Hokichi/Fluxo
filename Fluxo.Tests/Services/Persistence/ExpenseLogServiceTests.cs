using System;
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
        var (sut, unitOfWork, expenseLogs, _, spendingSources) = CreateSut();
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
        var (sut, unitOfWork, expenseLogs, _, spendingSources) = CreateSut();
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

    [Fact]
    public async Task PostTerminationCleanupAsync_RemovesMarkedLogs_WithoutRestoringBalancesAgain()
    {
        var (sut, unitOfWork, expenseLogs, expenses, spendingSources) = CreateSut();
        var markedLog = new ExpenseLog
        {
            Id = 101,
            ExpenseId = 30,
            SpendingSourceId = 7,
            Amount = 22m,
            IsForDeletion = true
        };

        expenseLogs.GetMarkedForDeletionAsync(Arg.Any<CancellationToken>())
            .Returns([markedLog]);
        expenseLogs.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ExpenseLog>());
        expenses.GetByExpenseIdAsync(markedLog.ExpenseId, Arg.Any<CancellationToken>())
            .Returns(new Expense { Id = markedLog.ExpenseId });

        await sut.PostTerminationCleanupAsync();

        expenseLogs.Received(1).Remove(markedLog);
        expenses.Received(1).Remove(Arg.Is<Expense>(expense => expense.Id == markedLog.ExpenseId));
        spendingSources.DidNotReceiveWithAnyArgs().Update(default!);
        await unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static (ExpenseLogService Sut, IUnitOfWork UnitOfWork, IExpenseLogRepository ExpenseLogs,
        IExpenseRepository Expenses, ISpendingSourceRepository SpendingSources) CreateSut()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var expenseLogs = Substitute.For<IExpenseLogRepository>();
        var expenses = Substitute.For<IExpenseRepository>();
        var spendingSources = Substitute.For<ISpendingSourceRepository>();

        unitOfWork.ExpenseLogs.Returns(expenseLogs);
        unitOfWork.Expenses.Returns(expenses);
        unitOfWork.SpendingSources.Returns(spendingSources);

        var sut = new ExpenseLogService(new InlineDataOperationRunner(unitOfWork), Substitute.For<IMapper>());
        return (sut, unitOfWork, expenseLogs, expenses, spendingSources);
    }
}
