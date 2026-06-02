using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.Persistence;
using Fluxo.ViewModels.Popups.Settings;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups.Settings;

public sealed class SettingsConfigTabVMTests
{
    [Fact]
    public void BudgetTab_ChangingAllocation_PublishesPendingState()
    {
        var messenger = new WeakReferenceMessenger();
        var captured = new List<SettingsPendingChangesChangedMessage>();
        var recipient = new PendingRecipient(captured);
        messenger.Register<PendingRecipient, SettingsPendingChangesChangedMessage>(recipient,
            static (r, m) => r.Messages.Add(m));

        var vm = new SettingsBudgetTabVM(() => 1000m, new AppDataService(new NullUnitOfWork()), messenger);
        vm.IncrementAllocation(BudgetAllocationSegment.Needs, 5);

        Assert.Contains(captured, m =>
            m.Value.TabKey == SettingsTabKey.Budget &&
            m.Value.HasPendingChanges);
    }

    [Fact]
    public void BudgetTab_InvalidAllocation_BlocksConfigurationSave()
    {
        var vm = new SettingsBudgetTabVM(() => 1000m, new AppDataService(new NullUnitOfWork()));

        vm.NeedsAllocationPercentage = 60;
        vm.WantsAllocationPercentage = 30;
        vm.InvestAllocationPercentage = 20;

        Assert.False(vm.CanSaveConfiguration);
        Assert.Equal(
            "Needs, Wants, and Invest must add up to 100%. Current total: 110%",
            vm.ConfigurationErrorMessage);
    }

    [Fact]
    public void BudgetTab_RevertChanges_RestoresLastSavedAllocation()
    {
        var vm = new SettingsBudgetTabVM(() => 1000m, new AppDataService(new NullUnitOfWork()));

        vm.NeedsAllocationPercentage = 60;
        vm.WantsAllocationPercentage = 20;
        vm.InvestAllocationPercentage = 20;

        vm.RevertChanges();

        Assert.Equal(50, vm.NeedsAllocationPercentage);
        Assert.Equal(30, vm.WantsAllocationPercentage);
        Assert.Equal(20, vm.InvestAllocationPercentage);
        Assert.False(vm.HasPendingChanges);
        Assert.True(vm.CanSaveConfiguration);
    }

    [Fact]
    public async Task BudgetTab_LoadAsync_LoadsTypedBudgetAllocation()
    {
        var unitOfWork = new NullUnitOfWork
        {
            BudgetAllocationEntity = new BudgetAllocation
            {
                NeedsThreshold = 40,
                WantsThreshold = 35,
                InvestThreshold = 25,
                AllocationLimit = 1400m,
                AllocationPeriod = AllocationPeriod.Weekly,
                RolloverPolicy = RolloverPolicy.Matching,
                OverspendPolicy = OverspendPolicy.HardStop
            }
        };
        var vm = new SettingsBudgetTabVM(() => 1000m, new AppDataService(unitOfWork));

        await vm.LoadAsync();

        Assert.Equal(40, vm.NeedsAllocationPercentage);
        Assert.Equal(35, vm.WantsAllocationPercentage);
        Assert.Equal(25, vm.InvestAllocationPercentage);
        Assert.Equal(1400m, vm.AllocationLimit);
        Assert.Equal(AllocationPeriod.Weekly, vm.AllocationPeriod);
        Assert.Equal(RolloverPolicy.Matching, vm.RolloverPolicy);
        Assert.Equal(OverspendPolicy.HardStop, vm.OverspendPolicy);
    }

    [Fact]
    public async Task BudgetTab_LoadAsync_DoesNotPublishTransientPendingState()
    {
        var messenger = new WeakReferenceMessenger();
        var captured = new List<SettingsPendingChangesChangedMessage>();
        var recipient = new PendingRecipient(captured);
        messenger.Register<PendingRecipient, SettingsPendingChangesChangedMessage>(recipient,
            static (r, m) => r.Messages.Add(m));
        var unitOfWork = new NullUnitOfWork
        {
            BudgetAllocationEntity = new BudgetAllocation
            {
                NeedsThreshold = 40,
                WantsThreshold = 35,
                InvestThreshold = 25,
                AllocationLimit = 1400m,
                AllocationPeriod = AllocationPeriod.Weekly,
                RolloverPolicy = RolloverPolicy.Matching,
                OverspendPolicy = OverspendPolicy.HardStop
            }
        };
        var vm = new SettingsBudgetTabVM(() => 1000m, new AppDataService(unitOfWork), messenger);

        await vm.LoadAsync();

        var budgetMessages = captured.Where(message => message.Value.TabKey == SettingsTabKey.Budget).ToList();
        Assert.DoesNotContain(budgetMessages, message => message.Value.HasPendingChanges);
        var finalMessage = Assert.Single(budgetMessages);
        Assert.False(finalMessage.Value.HasPendingChanges);
        Assert.False(vm.HasPendingChanges);
    }

    [Fact]
    public async Task BudgetTab_BuildApplyChangesAsync_UpdatesTypedBudgetAllocation()
    {
        var unitOfWork = new NullUnitOfWork();
        var vm = new SettingsBudgetTabVM(() => 1000m, new AppDataService(unitOfWork));
        await vm.LoadAsync();

        vm.AllocationLimit = 2100m;
        vm.AllocationPeriod = AllocationPeriod.Yearly;
        vm.RolloverPolicy = RolloverPolicy.Pooled;
        vm.OverspendPolicy = OverspendPolicy.SoftDebt;

        var (result, actions) = await vm.BuildApplyChangesAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(actions);
        Assert.NotNull(unitOfWork.BudgetAllocationEntity);
        Assert.Equal(2100m, unitOfWork.BudgetAllocationEntity.AllocationLimit);
        Assert.Equal(AllocationPeriod.Yearly, unitOfWork.BudgetAllocationEntity.AllocationPeriod);
        Assert.Equal(RolloverPolicy.Pooled, unitOfWork.BudgetAllocationEntity.RolloverPolicy);
        Assert.Equal(OverspendPolicy.SoftDebt, unitOfWork.BudgetAllocationEntity.OverspendPolicy);
    }

    [Fact]
    public void BudgetTab_ChangingConfiguration_PublishesPendingState()
    {
        var messenger = new WeakReferenceMessenger();
        var captured = new List<SettingsPendingChangesChangedMessage>();
        var recipient = new PendingRecipient(captured);
        messenger.Register<PendingRecipient, SettingsPendingChangesChangedMessage>(recipient,
            static (r, m) => r.Messages.Add(m));

        var vm = new SettingsBudgetTabVM(() => 1000m, new AppDataService(new NullUnitOfWork()), messenger);

        vm.AllocationLimit = 250m;
        Assert.Contains(captured, IsBudgetPendingMessage);

        captured.Clear();
        vm.AllocationPeriod = AllocationPeriod.Yearly;
        Assert.Contains(captured, IsBudgetPendingMessage);

        captured.Clear();
        vm.RolloverPolicy = RolloverPolicy.Pooled;
        Assert.Contains(captured, IsBudgetPendingMessage);

        captured.Clear();
        vm.OverspendPolicy = OverspendPolicy.SoftDebt;
        Assert.Contains(captured, IsBudgetPendingMessage);

        static bool IsBudgetPendingMessage(SettingsPendingChangesChangedMessage message)
        {
            return message.Value.TabKey == SettingsTabKey.Budget &&
                   message.Value.HasPendingChanges;
        }
    }

    [Fact]
    public void PersonalizationTab_ChangingStartupToggle_PublishesPendingState()
    {
        var messenger = new WeakReferenceMessenger();
        var captured = new List<SettingsPendingChangesChangedMessage>();
        var recipient = new PendingRecipient(captured);
        messenger.Register<PendingRecipient, SettingsPendingChangesChangedMessage>(recipient,
            static (r, m) => r.Messages.Add(m));

        var vm = new SettingsPersonalizationTabVM(new AppDataService(new NullUnitOfWork()), messenger);
        vm.ShouldRunAtStartup = true;

        Assert.Contains(captured, m =>
            m.Value.TabKey == SettingsTabKey.Personalization &&
            m.Value.HasPendingChanges);
    }

    private sealed class PendingRecipient(List<SettingsPendingChangesChangedMessage> messages)
    {
        public List<SettingsPendingChangesChangedMessage> Messages { get; } = messages;
    }

    private sealed class NullUnitOfWork : IUnitOfWork
    {
        public IExpenseRepository Expenses => throw new NotSupportedException();
        public IExpenseLogRepository ExpenseLogs => throw new NotSupportedException();
        public IIncomeLogRepository IncomeLogs => throw new NotSupportedException();
        public IExpenseTagRepository ExpenseTags => throw new NotSupportedException();
        public ISavingGoalRepository SavingGoals => throw new NotSupportedException();
        public ISpendingSourceRepository SpendingSources { get; } = new TestSpendingSourceRepository();
        public IRecurringTransactionRepository RecurringTransactions => throw new NotSupportedException();
        public INotificationRepository Notifications => throw new NotSupportedException();
        public IUserSettingsRepository UserSettings => throw new NotSupportedException();
        public IBudgetAllocationRepository BudgetAllocation => BudgetAllocationRepository;

        public BudgetAllocation? BudgetAllocationEntity
        {
            get => BudgetAllocationRepository.Entity;
            init => BudgetAllocationRepository.Entity = value;
        }

        private TestBudgetAllocationRepository BudgetAllocationRepository { get; } = new();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestBudgetAllocationRepository : IBudgetAllocationRepository
    {
        public BudgetAllocation? Entity { get; set; }

        public Task<BudgetAllocation?> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Entity);
        }

        public Task AddAsync(BudgetAllocation entity, CancellationToken cancellationToken = default)
        {
            Entity = entity;
            return Task.CompletedTask;
        }

        public void Update(BudgetAllocation entity)
        {
            Entity = entity;
        }
    }

    private sealed class TestSpendingSourceRepository : ISpendingSourceRepository
    {
        public Task<IReadOnlyList<SpendingSource>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SpendingSource> sources = [];
            return Task.FromResult(sources);
        }

        public Task<SpendingSource?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SpendingSource?>(null);
        }

        public Task AddAsync(SpendingSource entity, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Update(SpendingSource entity)
        {
        }

        public void Remove(SpendingSource entity)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task<IReadOnlyList<SpendingSource>> SearchAsync(
            SpendingSourceFilter filter,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SpendingSource> sources = [];
            return Task.FromResult(sources);
        }

        public Task<IReadOnlyList<SpendingSource>> GetMarkedForDeletionAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SpendingSource> sources = [];
            return Task.FromResult(sources);
        }
    }
}
