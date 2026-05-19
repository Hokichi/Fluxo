using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Enums;
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
        public ISpendingSourceRepository SpendingSources => throw new NotSupportedException();
        public IRecurringTransactionRepository RecurringTransactions => throw new NotSupportedException();
        public INotificationRepository Notifications => throw new NotSupportedException();
        public IUserSettingsRepository UserSettings => throw new NotSupportedException();

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
}
