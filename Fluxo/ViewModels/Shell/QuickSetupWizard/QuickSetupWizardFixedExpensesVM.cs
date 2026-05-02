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

    public AddFixedExpenseVM CreateAddViewModel()
    {
        var spendingSourcesVm = GetSpendingSourcesOrThrow();
        return new AddFixedExpenseVM(
            _mainViewModel,
            _appData,
            spendingSourcesOverride: spendingSourcesVm.BuildSpendingSourceOptions(),
            saveDraftAsync: input => SaveDraftExpenseAsync(input, null),
            loadDraftTagsAsync: _ => Task.FromResult<IReadOnlyList<ExpenseTagVM>>(BuildTagOptions()),
            createDraftTagAsync: CreateDraftTagAsync);
    }

    public async Task<AddFixedExpenseVM> CreateEditViewModelAsync(int id)
    {
        var spendingSourcesVm = GetSpendingSourcesOrThrow();

        if (!_isLoaded)
            await LoadDraftExpensesAsync();

        if (!_draftExpenses.TryGetValue(id, out var expense))
            return CreateAddViewModel();

        var vm = new AddFixedExpenseVM(
            _mainViewModel,
            _appData,
            spendingSourcesOverride: spendingSourcesVm.BuildSpendingSourceOptions(),
            saveDraftAsync: input => SaveDraftExpenseAsync(input, expense.Id),
            loadDraftTagsAsync: _ => Task.FromResult<IReadOnlyList<ExpenseTagVM>>(BuildTagOptions()),
            createDraftTagAsync: CreateDraftTagAsync)
        {
            EditingId = expense.Id
        };
        vm.NameText = expense.Name;
        vm.AmountText = expense.Amount;
        vm.SelectedCategory = expense.Category;
        vm.RecurringDateText = MonthlyDueDateHelper.Normalize(expense.RecurringDate)?.ToString(CultureInfo.InvariantCulture) ??
                               MonthlyDueDateHelper.Normalize(DateTime.Today.Day)?.ToString(CultureInfo.InvariantCulture) ??
                               MonthlyDueDateHelper.MinMonthlyDay.ToString(CultureInfo.InvariantCulture);
        vm.IsActive = expense.IsActive;

        if (expense.SpendingSourceId > 0)
        {
            var matchingSource = vm.SpendingSources.FirstOrDefault(s => s.Id == expense.SpendingSourceId);
            if (matchingSource is not null)
                vm.SelectedSpendingSource = matchingSource;
        }

        vm.TagNameText = expense.TagName;

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
        var existingExpenses = (await appData.GetExpensesAsync())
            .Where(expense => expense.ExpenseKind == ExpenseKind.Fixed)
            .ToDictionary(expense => expense.Id);

        foreach (var draft in _draftExpenses.Values.OrderBy(expense => expense.Id))
        {
            var mappedSourceId = sourceIdMap.TryGetValue(draft.SpendingSourceId, out var mapped)
                ? mapped
                : draft.SpendingSourceId;
            if (mappedSourceId <= 0)
                continue;

            var tagId = await ResolveTagIdAsync(appData, draft);

            if (draft.Id > 0 && existingExpenses.TryGetValue(draft.Id, out var persisted))
            {
                persisted.Name = draft.Name;
                persisted.Amount = draft.Amount;
                persisted.ExpenseCategory = draft.Category;
                persisted.RecurringDate = draft.RecurringDate;
                persisted.SpendingSourceId = mappedSourceId;
                persisted.ExpenseTagId = tagId;
                persisted.IsActive = draft.IsActive;
                appData.UpdateExpense(persisted);
            }
            else
            {
                await appData.AddExpenseAsync(new Expense
                {
                    Name = draft.Name,
                    Amount = draft.Amount,
                    ExpenseKind = ExpenseKind.Fixed,
                    ExpenseCategory = draft.Category,
                    RecurringDate = draft.RecurringDate,
                    SpendingSourceId = mappedSourceId,
                    ExpenseTagId = tagId,
                    IsActive = draft.IsActive
                });
            }
        }

        foreach (var removedId in _removedPersistedIds)
        {
            if (existingExpenses.TryGetValue(removedId, out var existing))
                appData.RemoveExpense(existing);
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
        var persistedExpenses = await _appData.GetExpensesAsync();

        _draftExpenses.Clear();

        foreach (var expense in persistedExpenses.Where(expense => expense.ExpenseKind == ExpenseKind.Fixed))
        {
            _draftExpenses[expense.Id] = new QuickSetupWizardDraftFixedExpense(
                expense.Id,
                expense.Name,
                expense.Amount,
                expense.ExpenseCategory,
                expense.SpendingSourceId,
                MonthlyDueDateHelper.Normalize(expense.RecurringDate) ??
                MonthlyDueDateHelper.Normalize(DateTime.Today.Day) ??
                MonthlyDueDateHelper.MinMonthlyDay,
                expense.ExpenseTagId,
                tagNamesById.TryGetValue(expense.ExpenseTagId, out var tagName)
                    ? tagName
                    : "General",
                expense.IsActive);
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
            input.RecurringDate,
            input.TagId,
            input.TagName,
            input.IsActive);

        if (id > 0)
            _removedPersistedIds.Remove(id);

        RefreshProjectionAndPublish();
        return Task.FromResult(AddFixedExpenseVM.AddFixedExpenseResult.Success(true));
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

