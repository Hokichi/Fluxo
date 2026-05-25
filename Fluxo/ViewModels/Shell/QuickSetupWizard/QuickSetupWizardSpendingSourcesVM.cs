using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Helpers;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public partial class QuickSetupWizardSpendingSourcesVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly IAppDataService _appData;
    private readonly IMessenger _messenger;
    private readonly Dictionary<int, QuickSetupWizardDraftSpendingSource> _draftSources = [];
    private readonly HashSet<int> _removedPersistedIds = [];
    private IReadOnlyDictionary<int, int> _lastPersistedIdMap = new Dictionary<int, int>();
    private int _nextTemporaryId = -1;
    private bool _isLoaded;

    [ObservableProperty] private bool _isStep2Active;

    public QuickSetupWizardSpendingSourcesVM(
        MainVM mainViewModel,
        IAppDataService appData,
        IMessenger? messenger = null)
    {
        _mainViewModel = mainViewModel;
        _appData = appData;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    public ObservableCollection<QuickSetupWizardSpendingSourceItemVM> SpendingSources { get; } = [];

    public bool HasSpendingSources => SpendingSources.Count > 0;

    public decimal TotalBudgetAmount => SpendingSources.Sum(source => source.PrimaryAmount);

    public IReadOnlyDictionary<int, int> LastPersistedIdMap => _lastPersistedIdMap;

    internal int GetNextTemporaryId()
    {
        return _nextTemporaryId--;
    }

    public string ResolveSourceName(int sourceId)
    {
        return _draftSources.TryGetValue(sourceId, out var source)
            ? source.Name
            : "No source";
    }

    public IReadOnlyList<SpendingSourceVM> BuildSpendingSourceOptions()
    {
        return _draftSources.Values
            .Where(source => source.IsEnabled)
            .OrderBy(source => source.Name)
            .Select(source => new SpendingSourceVM
            {
                Id = source.Id,
                Name = source.Name,
                SpendingSourceType = source.SpendingSourceType,
                Balance = source.Balance,
                SpentAmount = source.SpentAmount,
                AccountLimit = source.AccountLimit,
                MaximumSpending = source.MaximumSpending,
                MinimumPayment = source.MinimumPayment,
                MonthlyDueDate = source.MonthlyDueDate,
                DeductSource = source.DeductSource,
                InterestRate = source.InterestRate,
                ShowOnUI = source.ShowOnUi,
                IsEnabled = source.IsEnabled
            })
            .ToList();
    }

    public AddSpendingSourceVM CreateAddViewModel()
    {
        return new AddSpendingSourceVM(
            _mainViewModel,
            _appData,
            saveDraftAsync: input => SaveDraftSourceAsync(input, null),
            loadDraftDeductSourcesAsync: editingId =>
                Task.FromResult<IReadOnlyList<AddSpendingSourceVM.DeductSourceOption>>(
                    BuildDraftDeductSourceOptions(editingId)));
    }

    public async Task<AddSpendingSourceVM> CreateEditViewModelAsync(int id)
    {
        if (!_isLoaded)
            await LoadDraftSourcesAsync();

        if (!_draftSources.TryGetValue(id, out var source))
            return CreateAddViewModel();

        var vm = new AddSpendingSourceVM(
            _mainViewModel,
            _appData,
            saveDraftAsync: input => SaveDraftSourceAsync(input, source.Id),
            loadDraftDeductSourcesAsync: editingId =>
                Task.FromResult<IReadOnlyList<AddSpendingSourceVM.DeductSourceOption>>(
                    BuildDraftDeductSourceOptions(editingId)))
        {
            EditingId = source.Id
        };
        vm.NameText = source.Name;
        vm.SelectedSpendingSourceType = source.SpendingSourceType;
        vm.ShowOnUI = source.ShowOnUi;
        vm.IsEnabled = source.IsEnabled;

        if (source.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            vm.PrimaryAmountText = source.SpentAmount;
            vm.SpentAmountText = source.SpentAmount;
            vm.AccountLimitText = source.AccountLimit;
            vm.MaximumSpendingText = source.MaximumSpending;
            vm.MinimumPaymentText = source.MinimumPayment ?? 0m;
            vm.MonthlyDueDateText = MonthlyDueDateHelper.Normalize(source.MonthlyDueDate)?.ToString(CultureInfo.InvariantCulture) ??
                                    string.Empty;
            vm.SelectedDeductSource = source.DeductSource;
        }
        else
        {
            vm.PrimaryAmountText = source.Balance;
        }

        if (source.SpendingSourceType is not (SpendingSourceType.Credit or SpendingSourceType.BNPL))
            vm.MaximumSpendingText = source.MaximumSpending;

        if (source.SpendingSourceType == SpendingSourceType.Saving && source.InterestRate.HasValue)
            vm.ApyText = source.InterestRate.Value;

        return vm;
    }

    public Task DeleteAsync(int id)
    {
        if (id > 0)
            _removedPersistedIds.Add(id);

        _draftSources.Remove(id);

        foreach (var draft in _draftSources.Values.Where(source => source.DeductSource == id).ToList())
        {
            _draftSources[draft.Id] = draft with { DeductSource = null };
        }

        RefreshProjectionAndPublish();
        return Task.CompletedTask;
    }

    public string BuildDeleteConfirmationMessage(int id)
    {
        var sourceName = ResolveSourceName(id);
        var functioningSourceIds = _draftSources.Values
            .Where(source => SpendingSourceDeletionConfirmationHelper.IsFunctioning(
                source.SpendingSourceType,
                source.IsEnabled,
                source.Balance,
                source.AccountLimit))
            .Select(source => source.Id)
            .ToList();

        var isOnlyFunctioningSource = functioningSourceIds.Count == 1 && functioningSourceIds[0] == id;
        return SpendingSourceDeletionConfirmationHelper.BuildDeleteConfirmationMessage(sourceName, isOnlyFunctioningSource);
    }

    public async Task RefreshAsync()
    {
        if (!_isLoaded)
            await LoadDraftSourcesAsync();

        RefreshProjectionAndPublish();
    }

    public async Task ApplyAsync(IAppDataService appData)
    {
        var existingSources = (await appData.GetSpendingSourcesAsync())
            .ToDictionary(source => source.Id);
        var idMap = new Dictionary<int, int>();

        foreach (var draft in _draftSources.Values.OrderBy(source => source.Id))
        {
            if (draft.Id > 0 && existingSources.TryGetValue(draft.Id, out var persisted))
            {
                persisted.Name = draft.Name;
                persisted.SpendingSourceType = draft.SpendingSourceType;
                persisted.Balance = draft.Balance;
                persisted.SpentAmount = draft.SpentAmount;
                persisted.AccountLimit = draft.AccountLimit;
                persisted.MaximumSpending = draft.MaximumSpending;
                persisted.MinimumPayment = draft.MinimumPayment;
                persisted.MonthlyDueDate = draft.MonthlyDueDate;
                persisted.DeductSource = null;
                persisted.InterestRate = draft.InterestRate;
                persisted.ShowOnUI = draft.ShowOnUi;
                persisted.IsEnabled = draft.IsEnabled;
                appData.UpdateSpendingSource(persisted);
                idMap[draft.Id] = persisted.Id;
            }
            else
            {
                var created = new SpendingSource
                {
                    Name = draft.Name,
                    SpendingSourceType = draft.SpendingSourceType,
                    Balance = draft.Balance,
                    SpentAmount = draft.SpentAmount,
                    AccountLimit = draft.AccountLimit,
                    MaximumSpending = draft.MaximumSpending,
                    MinimumPayment = draft.MinimumPayment,
                    MonthlyDueDate = draft.MonthlyDueDate,
                    DeductSource = null,
                    InterestRate = draft.InterestRate,
                    ShowOnUI = draft.ShowOnUi,
                    IsEnabled = draft.IsEnabled
                };
                await appData.AddSpendingSourceAsync(created);
                await appData.SaveChangesAsync();
                idMap[draft.Id] = created.Id;
            }
        }

        foreach (var draft in _draftSources.Values)
        {
            if (!idMap.TryGetValue(draft.Id, out var persistedId))
                continue;

            var persisted = await appData.GetSpendingSourceByIdAsync(persistedId);
            if (persisted is null)
                continue;

            int? mappedDeductSource = null;
            if (draft.DeductSource.HasValue && idMap.TryGetValue(draft.DeductSource.Value, out var mapped))
                mappedDeductSource = mapped;

            persisted.DeductSource = mappedDeductSource;
            appData.UpdateSpendingSource(persisted);
        }

        var allExpenseLogs = await appData.GetExpenseLogsAsync();
        var allIncomeLogs = await appData.GetIncomeLogsAsync();

        foreach (var removedId in _removedPersistedIds)
        {
            if (existingSources.TryGetValue(removedId, out var existing))
            {
                var relatedExpenseLogs = allExpenseLogs
                    .Where(log => log.SpendingSourceId == removedId);
                foreach (var expenseLog in relatedExpenseLogs)
                    appData.RemoveExpenseLog(expenseLog);

                var relatedIncomeLogs = allIncomeLogs
                    .Where(log => log.SpendingSourceId == removedId);
                foreach (var incomeLog in relatedIncomeLogs)
                    appData.RemoveIncomeLog(incomeLog);

                appData.RemoveSpendingSource(existing);
            }
        }

        _lastPersistedIdMap = new Dictionary<int, int>(idMap);
    }

    private void PublishSnapshot()
    {
        _messenger.Send(new QuickSetupWizardSpendingSourcesChangedMessage(
            new QuickSetupWizardSpendingSourcesChanged(
                SpendingSources.Count,
                HasSpendingSources,
                TotalBudgetAmount)));
    }

    private async Task LoadDraftSourcesAsync()
    {
        var persistedSources = await _appData.GetSpendingSourcesAsync();
        _draftSources.Clear();

        foreach (var source in persistedSources)
        {
            _draftSources[source.Id] = new QuickSetupWizardDraftSpendingSource(
                source.Id,
                source.Name,
                source.SpendingSourceType,
                source.Balance,
                source.SpentAmount,
                source.AccountLimit,
                source.MaximumSpending,
                source.MinimumPayment,
                source.MonthlyDueDate,
                source.DeductSource,
                source.InterestRate,
                source.ShowOnUI,
                source.IsEnabled);
        }

        _removedPersistedIds.Clear();
        _lastPersistedIdMap = new Dictionary<int, int>();
        _nextTemporaryId = -1;
        _isLoaded = true;
    }

    private Task<AddSpendingSourceVM.AddSpendingSourceResult> SaveDraftSourceAsync(
        AddSpendingSourceVM.AddSpendingSourceInput input,
        int? editingId)
    {
        if (_draftSources.Values.Any(source =>
                source.Id != (editingId ?? int.MinValue) &&
                string.Equals(source.Name, input.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(AddSpendingSourceVM.AddSpendingSourceResult.Failure(
                $"A spending source named \"{input.Name}\" already exists."));
        }

        var id = editingId ?? GetNextTemporaryId();
        _draftSources[id] = new QuickSetupWizardDraftSpendingSource(
            id,
            input.Name,
            input.SpendingSourceType,
            input.Balance,
            input.SpentAmount,
            input.AccountLimit,
            input.MaximumSpending,
            input.MinimumPayment,
            input.MonthlyDueDate,
            input.DeductSource,
            input.InterestRate,
            input.ShowOnUI,
            input.IsEnabled);

        if (id > 0)
            _removedPersistedIds.Remove(id);

        RefreshProjectionAndPublish();
        return Task.FromResult(AddSpendingSourceVM.AddSpendingSourceResult.Success(true));
    }

    private IReadOnlyList<AddSpendingSourceVM.DeductSourceOption> BuildDraftDeductSourceOptions(int? editingId)
    {
        return _draftSources.Values
            .Where(source => source.Id != (editingId ?? 0))
            .Where(source => source.SpendingSourceType is not (SpendingSourceType.Credit or SpendingSourceType.BNPL))
            .OrderBy(source => source.Name)
            .Select(source => new AddSpendingSourceVM.DeductSourceOption(source.Id, source.Name))
            .ToList();
    }

    private void RefreshProjectionAndPublish()
    {
        QuickSetupWizardShared.ReplaceCollection(
            SpendingSources,
            _draftSources.Values
                .OrderBy(source => source.Name)
                .Select(source => new QuickSetupWizardSpendingSourceItemVM(source)));

        OnPropertyChanged(nameof(HasSpendingSources));
        OnPropertyChanged(nameof(TotalBudgetAmount));
        PublishSnapshot();
    }
}

