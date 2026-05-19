using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Services.Persistence;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell.QuickSetupWizard;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class QuickSetupWizardVMTests
{
    [Fact]
    public void CurrentStepTitle_AtStep1_AsksForPreferredName()
    {
        var viewModel = CreateViewModel();
        viewModel.CurrentStepIndex = 1;

        Assert.Equal("What should fluxo call you?", viewModel.NamePage.CurrentStepTitle);
    }

    [Fact]
    public void CurrentStepDescription_AtStep1_DoesNotMentionSalary()
    {
        var viewModel = CreateViewModel();
        viewModel.CurrentStepIndex = 1;

        Assert.DoesNotContain("salary", viewModel.NamePage.CurrentStepDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GoNextAsync_OnStep1_DoesNotOverwriteExistingSalarySetting()
    {
        var userSettingsRepository = new TestUserSettingsRepository(
        [
            new UserSettings { Name = UserSettingNames.PreferredDisplayName, Value = "Existing Name" },
            new UserSettings { Name = UserSettingNames.Salary, Value = "6000" }
        ]);

        var unitOfWork = new TestUnitOfWork(userSettingsRepository);
        var viewModel = CreateViewModel(unitOfWork);
        viewModel.CurrentStepIndex = 1;
        viewModel.NamePage.UsernameText = "Alex";

        var result = await viewModel.GoNextAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, viewModel.CurrentStepIndex);
        Assert.Equal("6000", userSettingsRepository.GetValue(UserSettingNames.Salary));
        Assert.DoesNotContain(UserSettingNames.Salary, userSettingsRepository.AddedNames);
        Assert.DoesNotContain(UserSettingNames.Salary, userSettingsRepository.UpdatedNames);
        Assert.DoesNotContain(UserSettingNames.Salary, userSettingsRepository.RemovedNames);
    }

    [Fact]
    public async Task ExecuteLoadingFlowAsync_UserDeclinesAfterFiveFailures_ReturnsAbandoned()
    {
        var viewModel = CreateViewModel();
        var attempts = 0;
        var prompts = 0;

        var outcome = await viewModel.ExecuteLoadingFlowAsync(
            tryStageAsyncOverride: () =>
            {
                attempts++;
                return Task.FromResult(false);
            },
            confirmRetryCycleAsync: () =>
            {
                prompts++;
                return Task.FromResult(false);
            },
            delayAsync: _ => Task.CompletedTask);

        Assert.Equal(QuickSetupWizardLoadingOutcome.Abandoned, outcome);
        Assert.Equal(5, attempts);
        Assert.Equal(1, prompts);
    }

    private static QuickSetupWizardVM CreateViewModel(TestUnitOfWork? unitOfWork = null)
    {
        unitOfWork ??= new TestUnitOfWork(new TestUserSettingsRepository([]));
        var appData = new AppDataService(unitOfWork);
        var messenger = new WeakReferenceMessenger();
        var greeting = new QuickSetupWizardGreetingPageVM();
        var name = new QuickSetupWizardNamePageVM(appData, messenger);
        var spendingSources = new QuickSetupWizardSpendingSourcesVM(null!, appData, messenger);
        var fixedExpenses = new QuickSetupWizardFixedExpensesVM(null!, appData, messenger);
        var savingGoals = new QuickSetupWizardSavingGoalsVM(null!, appData, messenger);
        var budget = new QuickSetupWizardBudgetAllocationVM(appData, messenger);
        var notification = new QuickSetupWizardNotificationVM(appData, messenger);
        var summary = new QuickSetupWizardSummaryVM(messenger);
        var middle = new QuickSetupWizardMiddlePageVM(
            spendingSources,
            fixedExpenses,
            savingGoals,
            budget,
            notification,
            summary,
            messenger);
        var loading = new QuickSetupWizardLoadingPageVM();
        var final = new QuickSetupWizardFinalPageVM(messenger);
        return new QuickSetupWizardVM(
            null!,
            appData,
            Substitute.For<IStartupRegistrationService>(),
            new TestDataOperationScopeFactory(),
            greeting,
            name,
            middle,
            loading,
            final,
            messenger);
    }

    private sealed class TestDataOperationScopeFactory : IDataOperationScopeFactory
    {
        public ValueTask<IDataOperationScope> CreateAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestUnitOfWork(TestUserSettingsRepository userSettingsRepository) : IUnitOfWork
    {
        public IExpenseRepository Expenses => throw new NotSupportedException();
        public IExpenseLogRepository ExpenseLogs => throw new NotSupportedException();
        public IIncomeLogRepository IncomeLogs => throw new NotSupportedException();
        public IExpenseTagRepository ExpenseTags => throw new NotSupportedException();
        public ISavingGoalRepository SavingGoals => throw new NotSupportedException();
        public ISpendingSourceRepository SpendingSources => throw new NotSupportedException();
        public IRecurringTransactionRepository RecurringTransactions => throw new NotSupportedException();
        public INotificationRepository Notifications => throw new NotSupportedException();
        public IUserSettingsRepository UserSettings => userSettingsRepository;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
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

    private sealed class TestUserSettingsRepository(IReadOnlyList<UserSettings> initialSettings) : IUserSettingsRepository
    {
        private readonly Dictionary<string, UserSettings> _settings = initialSettings
            .ToDictionary(setting => setting.Name, setting => new UserSettings { Name = setting.Name, Value = setting.Value }, StringComparer.Ordinal);

        public HashSet<string> AddedNames { get; } = new(StringComparer.Ordinal);
        public HashSet<string> UpdatedNames { get; } = new(StringComparer.Ordinal);
        public HashSet<string> RemovedNames { get; } = new(StringComparer.Ordinal);

        public Task<IReadOnlyList<UserSettings>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<UserSettings>>(_settings.Values
                .Select(setting => new UserSettings { Name = setting.Name, Value = setting.Value })
                .ToList());
        }

        public Task<UserSettings?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            if (_settings.TryGetValue(name, out var setting))
                return Task.FromResult<UserSettings?>(new UserSettings { Name = setting.Name, Value = setting.Value });

            return Task.FromResult<UserSettings?>(null);
        }

        public Task AddAsync(UserSettings entity, CancellationToken cancellationToken = default)
        {
            _settings[entity.Name] = new UserSettings { Name = entity.Name, Value = entity.Value };
            AddedNames.Add(entity.Name);
            return Task.CompletedTask;
        }

        public void Update(UserSettings entity)
        {
            _settings[entity.Name] = new UserSettings { Name = entity.Name, Value = entity.Value };
            UpdatedNames.Add(entity.Name);
        }

        public void Remove(UserSettings entity)
        {
            _settings.Remove(entity.Name);
            RemovedNames.Add(entity.Name);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1);
        }

        public string? GetValue(string name)
        {
            return _settings.TryGetValue(name, out var setting) ? setting.Value : null;
        }
    }
}
