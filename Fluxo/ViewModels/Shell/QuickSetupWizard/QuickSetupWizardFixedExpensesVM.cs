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
using Fluxo.ViewModels.Popups.Settings;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;
using Fluxo.ViewModels.Popups.Helpers;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public partial class QuickSetupWizardFixedExpensesVM : ObservableObject
{
    private const string DefaultTagColor = "#75B798";

    private readonly MainVM _mainViewModel;
    private readonly IAppDataService _appData;
    private readonly IMessenger _messenger;
    private QuickSetupWizardSpendingSourcesVM? _spendingSources;
    private readonly Dictionary<int, QuickSetupWizardDraftFixedExpense> _draftExpenses = [];
    private readonly HashSet<int> _removedPersistedIds = [];
    private readonly Dictionary<int, ExpenseTagVM> _tagCatalog = [];
    private int _nextTemporaryId = -1;
    private int _nextDraftTagId = -1;
    private bool _isLoaded;

    [ObservableProperty] private bool _isStep3Active;

    public QuickSetupWizardFixedExpensesVM(
        MainVM mainViewModel,
        IAppDataService appData,
        IMessenger? messenger = null)
    {
        _mainViewModel = mainViewModel;
        _appData = appData;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    public ObservableCollection<QuickSetupWizardFixedExpenseItemVM> FixedExpenses { get; } = [];

    public void SetSpendingSources(QuickSetupWizardSpendingSourcesVM spendingSources)
    {
        _spendingSources = spendingSources;
    }

    public AddNewTransactionVM CreateAddViewModel()
    {
        var spendingSourcesVm = GetSpendingSourcesOrThrow();
        var vm = new AddNewTransactionVM(
            _mainViewModel,
            _appData,
            spendingSourcesOverride: spendingSourcesVm.BuildSpendingSourceOptions(),
            saveRecurringDraftAsync: SaveDraftRecurringTransactionAsync);
        vm.InitializeRecurringMode(isLocked: true);
        return vm;
    }

    public async Task<AddNewTransactionVM> CreateEditViewModelAsync(int id)
    {
        if (!_isLoaded)
            await LoadDraftExpensesAsync();

        var spendingSourcesVm = GetSpendingSourcesOrThrow();
        var vm = new AddNewTransactionVM(
            _mainViewModel,
            _appData,
            spendingSourcesOverride: spendingSourcesVm.BuildSpendingSourceOptions(),
            saveRecurringDraftAsync: SaveDraftRecurringTransactionAsync);

        if (_draftExpenses.TryGetValue(id, out var draft))
        {
            vm.InitializeFromRecurringDraft(new AddNewTransactionVM.RecurringDraftSnapshot(
                draft.Id > 0 ? draft.Id : null,
                RecurringTransactionType.Expense,
                draft.Name,
                draft.Amount,
                draft.RecurringPeriod,
                draft.RecurringTime,
                draft.SpendingSourceId,
                draft.ExpenseTagId > 0 ? draft.ExpenseTagId : null,
                null));
            return vm;
        }

        await vm.InitializeFromRecurringTransactionAsync(id);
        return vm;
    }

    public Task DeleteAsync(int id)
    {
        if (id > 0)
            _removedPersistedIds.Add(id);

        _draftExpenses.Remove(id);
        RefreshProjectionAndPublish();
        return Task.CompletedTask;
    }

    public async Task RefreshAsync()
    {
        if (!_isLoaded)
            await LoadDraftExpensesAsync();

        RefreshProjectionAndPublish();
    }

    public async Task ApplyAsync(IAppDataService appData, IReadOnlyDictionary<int, int> sourceIdMap)
    {
        var existingRecurring = (await appData.GetRecurringTransactionsAsync())
            .ToDictionary(expense => expense.Id);

        foreach (var draft in _draftExpenses.Values.OrderBy(expense => expense.Id))
        {
            var mappedSourceId = sourceIdMap.TryGetValue(draft.SpendingSourceId, out var mapped)
                ? mapped
                : draft.SpendingSourceId;
            if (mappedSourceId <= 0)
                continue;

            var tagId = await ResolveTagIdAsync(appData, draft);

            if (draft.Id > 0 && existingRecurring.TryGetValue(draft.Id, out var persisted))
            {
                persisted.Name = draft.Name;
                persisted.Amount = draft.Amount;
                persisted.SourceId = mappedSourceId;
                persisted.TagId = tagId;
                persisted.RecurringPeriod = draft.RecurringPeriod;
                persisted.RecurringTime = draft.RecurringTime;
                appData.UpdateRecurringTransaction(persisted);
            }
            else
            {
                await appData.AddRecurringTransactionAsync(new RecurringTransaction
                {
                    Name = draft.Name,
                    Amount = draft.Amount,
                    RecurringPeriod = draft.RecurringPeriod,
                    RecurringTime = draft.RecurringTime,
                    Type = RecurringTransactionType.Expense,
                    SourceId = mappedSourceId,
                    TagId = tagId,
                    IsEnabled = true
                });
            }
        }

        foreach (var removedId in _removedPersistedIds)
        {
            if (existingRecurring.TryGetValue(removedId, out var existing))
                appData.RemoveRecurringTransaction(existing);
        }
    }

    private void PublishSnapshot()
    {
        _messenger.Send(new QuickSetupWizardFixedExpensesChangedMessage(
            new QuickSetupWizardFixedExpensesChanged(
                FixedExpenses.Count,
                FixedExpenses.Sum(expense => expense.Amount))));
    }

    private async Task LoadDraftExpensesAsync()
    {
        var persistedTags = await _appData.GetExpenseTagsAsync();
        _tagCatalog.Clear();
        foreach (var tag in persistedTags.Where(tag => !tag.IsSystemTag))
        {
            _tagCatalog[tag.Id] = new ExpenseTagVM
            {
                Id = tag.Id,
                Name = tag.Name,
                HexCode = tag.HexCode,
                IsSystemTag = tag.IsSystemTag
            };
        }

        var tagNamesById = _tagCatalog.Values.ToDictionary(tag => tag.Id, tag => tag.Name);
        var persistedExpenses = await _appData.GetRecurringTransactionsAsync();

        _draftExpenses.Clear();

        foreach (var expense in persistedExpenses.Where(item => item.Type == RecurringTransactionType.Expense))
        {
            _draftExpenses[expense.Id] = new QuickSetupWizardDraftFixedExpense(
                expense.Id,
                expense.Name,
                expense.Amount,
                ExpenseCategory.Needs,
                expense.SourceId,
                expense.RecurringPeriod,
                expense.RecurringTime,
                expense.TagId ?? 0,
                expense.TagId.HasValue && tagNamesById.TryGetValue(expense.TagId.Value, out var tagName)
                    ? tagName
                    : "General",
                expense.IsEnabled);
        }

        _removedPersistedIds.Clear();
        _nextTemporaryId = -1;
        _nextDraftTagId = -1;
        _isLoaded = true;
    }

    private Task<AddFixedExpenseVM.AddFixedExpenseResult> SaveDraftExpenseAsync(
        AddFixedExpenseVM.AddFixedExpenseInput input,
        int? editingId)
    {
        var id = editingId ?? _nextTemporaryId--;
        _draftExpenses[id] = new QuickSetupWizardDraftFixedExpense(
            id,
            input.Name,
            input.Amount,
            input.Category,
            input.SpendingSourceId,
            RecurringPeriod.Monthly,
            input.RecurringTime,
            input.TagId,
            input.TagName,
            input.IsActive);

        if (id > 0)
            _removedPersistedIds.Remove(id);

        RefreshProjectionAndPublish();
        return Task.FromResult(AddFixedExpenseVM.AddFixedExpenseResult.Success(true));
    }

    private Task<AddNewTransactionVM.AddNewTransactionSubmissionResult> SaveDraftRecurringTransactionAsync(
        AddNewTransactionVM.RecurringDraftSaveInput input)
    {
        if (input.Type != RecurringTransactionType.Expense)
        {
            return Task.FromResult(AddNewTransactionVM.AddNewTransactionSubmissionResult.Failure(
                "Startup Wizard recurring transactions currently support expenses only."));
        }

        var id = input.EditingRecurringTransactionId ?? _nextTemporaryId--;
        var tagName = ResolveDraftTagName(input.TagId);

        _draftExpenses[id] = new QuickSetupWizardDraftFixedExpense(
            id,
            input.Name,
            input.Amount,
            ExpenseCategory.Needs,
            input.SpendingSourceId,
            input.RecurringPeriod,
            input.RecurringTime,
            input.TagId ?? 0,
            tagName,
            true);

        if (id > 0)
            _removedPersistedIds.Remove(id);

        RefreshProjectionAndPublish();
        return Task.FromResult(AddNewTransactionVM.AddNewTransactionSubmissionResult.Success());
    }

    private string ResolveDraftTagName(int? tagId)
    {
        if (tagId.HasValue && _tagCatalog.TryGetValue(tagId.Value, out var tag))
            return tag.Name;

        return "General";
    }

    private async Task<int> ResolveTagIdAsync(IAppDataService appData, QuickSetupWizardDraftFixedExpense draft)
    {
        if (draft.ExpenseTagId > 0)
        {
            var existingById = await appData.GetExpenseTagByIdAsync(draft.ExpenseTagId);
            if (existingById is not null && !existingById.IsSystemTag)
                return existingById.Id;
        }

        var desiredTagName = string.IsNullOrWhiteSpace(draft.TagName)
            ? "General"
            : draft.TagName.Trim();

        var existingTags = await appData.GetExpenseTagsAsync();
        var existingByName = existingTags.FirstOrDefault(tag =>
            !tag.IsSystemTag &&
            string.Equals(tag.Name, desiredTagName, StringComparison.OrdinalIgnoreCase));
        if (existingByName is not null)
            return existingByName.Id;

        var createdTag = new ExpenseTag
        {
            Name = desiredTagName,
            HexCode = DefaultTagColor
        };
        await appData.AddExpenseTagAsync(createdTag);
        await appData.SaveChangesAsync();
        return createdTag.Id;
    }

    private Task<SettingsOperationResult> CreateDraftTagAsync(string name, string hexCode)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        var normalizedHexCode = NormalizeHexColor(hexCode);

        if (trimmedName.Length == 0)
            return Task.FromResult(SettingsOperationResult.Failure("Please enter a tag name."));

        if (!IsHexColor(normalizedHexCode))
            return Task.FromResult(SettingsOperationResult.Failure("Please choose a valid tag color."));

        if (_tagCatalog.Values.Any(tag => string.Equals(tag.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult(SettingsOperationResult.Failure($"A tag named \"{trimmedName}\" already exists."));

        _tagCatalog[_nextDraftTagId] = new ExpenseTagVM
        {
            Id = _nextDraftTagId,
            Name = trimmedName,
            HexCode = normalizedHexCode,
            IsSystemTag = false
        };
        _nextDraftTagId--;

        return Task.FromResult(SettingsOperationResult.Success());
    }

    private IReadOnlyList<ExpenseTagVM> BuildTagOptions()
    {
        return _tagCatalog.Values
            .Where(tag => !tag.IsSystemTag)
            .OrderBy(tag => tag.Name)
            .Select(tag => new ExpenseTagVM
            {
                Id = tag.Id,
                Name = tag.Name,
                HexCode = tag.HexCode,
                IsSystemTag = tag.IsSystemTag
            })
            .ToList();
    }

    private static string NormalizeHexColor(string hexCode)
    {
        var normalized = (hexCode ?? string.Empty).Trim().TrimStart('#').ToUpperInvariant();
        return $"#{normalized}";
    }

    private static bool IsHexColor(string hexCode)
    {
        var normalized = NormalizeHexColor(hexCode);
        return normalized.Length == 7 &&
               normalized[0] == '#' &&
               normalized.Skip(1).All(static character =>
                   char.IsDigit(character) || (character >= 'A' && character <= 'F'));
    }

    private void RefreshProjectionAndPublish()
    {
        var spendingSourcesVm = GetSpendingSourcesOrThrow();

        QuickSetupWizardShared.ReplaceCollection(
            FixedExpenses,
            _draftExpenses.Values
                .OrderBy(expense => expense.Name)
                .Select(expense => new QuickSetupWizardFixedExpenseItemVM(
                    expense,
                    spendingSourcesVm.ResolveSourceName(expense.SpendingSourceId))));

        PublishSnapshot();
    }

    private QuickSetupWizardSpendingSourcesVM GetSpendingSourcesOrThrow()
    {
        return _spendingSources
               ?? throw new InvalidOperationException("Startup wizard spending sources were not configured.");
    }
}

