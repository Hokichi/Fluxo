using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.ViewModels.Popups;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class StartupWizardVMTests
{
    [Fact]
    public void CurrentStepTitle_AtStep1_AsksForPreferredName()
    {
        var viewModel = CreateViewModel();
        viewModel.CurrentStepIndex = 1;

        Assert.Equal("What should Fluxo call you?", viewModel.CurrentStepTitle);
    }

    [Fact]
    public void CurrentStepDescription_AtStep1_DoesNotMentionSalary()
    {
        var viewModel = CreateViewModel();
        viewModel.CurrentStepIndex = 1;

        Assert.DoesNotContain("salary", viewModel.CurrentStepDescription, StringComparison.OrdinalIgnoreCase);
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
        viewModel.UsernameText = "Alex";

        var result = await viewModel.GoNextAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, viewModel.CurrentStepIndex);
        Assert.Equal("6000", userSettingsRepository.GetValue(UserSettingNames.Salary));
        Assert.DoesNotContain(UserSettingNames.Salary, userSettingsRepository.AddedNames);
        Assert.DoesNotContain(UserSettingNames.Salary, userSettingsRepository.UpdatedNames);
        Assert.DoesNotContain(UserSettingNames.Salary, userSettingsRepository.RemovedNames);
    }

    private static StartupWizardVM CreateViewModel(TestUnitOfWork? unitOfWork = null)
    {
        unitOfWork ??= new TestUnitOfWork(new TestUserSettingsRepository([]));
        return new StartupWizardVM(null!, unitOfWork);
    }

    private sealed class TestUnitOfWork(TestUserSettingsRepository userSettingsRepository) : IUnitOfWork
    {
        public IExpenseRepository Expenses => throw new NotSupportedException();
        public IExpenseLogRepository ExpenseLogs => throw new NotSupportedException();
        public IIncomeLogRepository IncomeLogs => throw new NotSupportedException();
        public IExpenseTagRepository ExpenseTags => throw new NotSupportedException();
        public ISavingGoalRepository SavingGoals => throw new NotSupportedException();
        public ISpendingSourceRepository SpendingSources => throw new NotSupportedException();
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
