using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Services.Persistence;
using Fluxo.ViewModels.Popups;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class GoalUpdateTransactionSupportTests
{
    [Fact]
    public async Task ResolveGoalUpdateTagAsync_ReturnsExistingTag_WhenItAlreadyExists()
    {
        var existingTag = new ExpenseTag
        {
            Id = 42,
            Name = "Goal Update",
            HexCode = "#ffffff"
        };

        var expenseTagRepository = new TestExpenseTagRepository([existingTag]);
        var unitOfWork = new TestUnitOfWork(expenseTagRepository);

        var resolvedTag = await GoalUpdateTransactionSupport.ResolveGoalUpdateTagAsync(new AppDataService(unitOfWork));

        Assert.Equal(existingTag.Id, resolvedTag.Id);
        Assert.Equal(1, expenseTagRepository.Tags.Count);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ResolveGoalUpdateTagAsync_CreatesGoalUpdateTag_WhenMissing()
    {
        var expenseTagRepository = new TestExpenseTagRepository([]);
        var unitOfWork = new TestUnitOfWork(expenseTagRepository);

        var resolvedTag = await GoalUpdateTransactionSupport.ResolveGoalUpdateTagAsync(new AppDataService(unitOfWork));

        Assert.Equal("Goal Update", resolvedTag.Name);
        Assert.Equal("#aed4e1", resolvedTag.HexCode);
        Assert.Single(expenseTagRepository.Tags);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    private sealed class TestUnitOfWork(TestExpenseTagRepository expenseTags) : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public IExpenseRepository Expenses => throw new NotSupportedException();
        public IExpenseLogRepository ExpenseLogs => throw new NotSupportedException();
        public IIncomeLogRepository IncomeLogs => throw new NotSupportedException();
        public IExpenseTagRepository ExpenseTags => expenseTags;
        public ISavingGoalRepository SavingGoals => throw new NotSupportedException();
        public ISpendingSourceRepository SpendingSources => throw new NotSupportedException();
        public IRecurringTransactionRepository RecurringTransactions => throw new NotSupportedException();
        public INotificationRepository Notifications => throw new NotSupportedException();
        public IUserSettingsRepository UserSettings => throw new NotSupportedException();
        public IBudgetAllocationRepository BudgetAllocation => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestExpenseTagRepository(IReadOnlyList<ExpenseTag> initialTags) : IExpenseTagRepository
    {
        private int _nextId = initialTags.Count == 0 ? 1 : initialTags.Max(tag => tag.Id) + 1;

        public List<ExpenseTag> Tags { get; } = [.. initialTags];

        public Task<IReadOnlyList<ExpenseTag>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ExpenseTag>>(Tags);
        }

        public Task<ExpenseTag?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Tags.FirstOrDefault(tag => tag.Id == id));
        }

        public Task AddAsync(ExpenseTag entity, CancellationToken cancellationToken = default)
        {
            if (entity.Id <= 0)
                entity.Id = _nextId++;

            Tags.Add(entity);
            return Task.CompletedTask;
        }

        public void Update(ExpenseTag entity)
        {
            var index = Tags.FindIndex(tag => tag.Id == entity.Id);
            if (index >= 0)
                Tags[index] = entity;
        }

        public void Remove(ExpenseTag entity)
        {
            Tags.RemoveAll(tag => tag.Id == entity.Id);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1);
        }

        public Task<IReadOnlyList<(ExpenseTag Tag, int Count)>> GetTagsByCountDescendingAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<(ExpenseTag Tag, int Count)>>([]);
        }

        public Task<IReadOnlyList<(ExpenseTag Tag, int Count)>> GetTodayTagsByCountDescendingAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<(ExpenseTag Tag, int Count)>>([]);
        }
    }
}
