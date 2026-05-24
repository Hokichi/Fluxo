using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.Logging;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups.Helpers;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Popups;

public partial class IncomeDetailVM : ObservableObject
{
    private readonly IAppDataService _appData;
    private readonly IncomeLogVM _incomeLog;
    private readonly MainVM _mainViewModel;
    private readonly List<SpendingSourceVM> _availableSpendingSources = [];

    [ObservableProperty] private decimal _amountText;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _noteText = string.Empty;
    [ObservableProperty] private string _popupTitle = "Income Detail";
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private SpendingSourceVM? _selectedSpendingSource;

    private IncomeDetailSavedState _savedState = new(string.Empty, 0m, string.Empty, DateTime.Today, 0);

    public IncomeDetailVM(MainVM mainViewModel, IncomeLogVM incomeLog, IAppDataService appData)
    {
        _mainViewModel = mainViewModel;
        _incomeLog = incomeLog;
        _appData = appData;
        SpendingSourcesView = SpendingSourceComboBoxViewFactory.CreateGroupedByTypeThenName(
            SpendingSources,
            nameof(SpendingSourceVM.TypeDisplayName),
            nameof(SpendingSourceVM.SpendingSourceType),
            nameof(SpendingSourceVM.Name));

        ReloadChoicesFromMainViewModel();
        _savedState = CreateSavedState(incomeLog);
        LoadFromSavedState();
    }

    public ObservableCollection<SpendingSourceVM> SpendingSources { get; } = [];
    public ICollectionView SpendingSourcesView { get; }

    public bool AreFieldsReadOnly => !IsEditing;
    public bool CanEditFields => IsEditing;

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(AreFieldsReadOnly));
        OnPropertyChanged(nameof(CanEditFields));
    }

    public Task BeginEditingAsync()
    {
        IsEditing = true;
        return Task.CompletedTask;
    }

    public void CancelEditing()
    {
        IsEditing = false;
        LoadFromSavedState();
    }

    public QuickAddVM.QuickAddDraft CreateQuickAddDraft()
    {
        return new QuickAddVM.QuickAddDraft(
            false,
            NameText,
            AmountText,
            SelectedSpendingSource?.Id,
            SelectedDate.Date,
            NoteText,
            null,
            null);
    }

    public async Task<IncomeDetailSaveResult> SaveAsync()
    {
        if (IsSaving)
            return IncomeDetailSaveResult.Failure("This income is already being saved.");

        if (!TryBuildInput(out var input, out var validationMessage))
            return IncomeDetailSaveResult.Failure(validationMessage);

        if (GetChangedFields(input, _savedState) == IncomeDetailChangedFields.None)
        {
            IsEditing = false;
            LoadFromSavedState();
            return IncomeDetailSaveResult.Success();
        }

        IsSaving = true;

        try
        {
            var incomeLog = await _appData.GetIncomeLogByIdAsync(_incomeLog.Id);
            if (incomeLog is null)
                return IncomeDetailSaveResult.Failure("Unable to load this income.");

            var currentSpendingSource = incomeLog.SpendingSource;
            if (currentSpendingSource is null)
                return IncomeDetailSaveResult.Failure("Unable to load this income source.");

            var newSpendingSource = await _appData.GetSpendingSourceByIdAsync(input.SpendingSourceId);
            if (newSpendingSource is null)
                return IncomeDetailSaveResult.Failure("Please select a valid spending source.");

            var resolvedName = BuildIncomeName(input.Name, input.Note);
            var sourceChanged = currentSpendingSource.Id != newSpendingSource.Id;
            if (!sourceChanged)
            {
                RevertIncomeFromSpendingSource(currentSpendingSource, incomeLog.Amount);
                ApplyIncomeToSpendingSource(currentSpendingSource, input.Amount);
                newSpendingSource = currentSpendingSource;
            }
            else
            {
                RevertIncomeFromSpendingSource(currentSpendingSource, incomeLog.Amount);
                ApplyIncomeToSpendingSource(newSpendingSource, input.Amount);
            }

            incomeLog.Name = resolvedName;
            incomeLog.Amount = input.Amount;
            incomeLog.AddedOn = input.Date;
            incomeLog.Notes = input.Note;
            incomeLog.SpendingSourceId = input.SpendingSourceId;
            incomeLog.SpendingSource = newSpendingSource;

            _appData.UpdateIncomeLog(incomeLog);
            _appData.UpdateSpendingSource(currentSpendingSource);

            if (sourceChanged)
                _appData.UpdateSpendingSource(newSpendingSource);

            await _appData.SaveChangesAsync();
            _savedState = new IncomeDetailSavedState(
                resolvedName,
                input.Amount,
                input.Note,
                input.Date,
                input.SpendingSourceId);

            IsEditing = false;
            LoadFromSavedState();
            WeakReferenceMessenger.Default.Send(
                new DashboardDataInvalidatedMessage(DashboardDataInvalidationScope.All));
            return IncomeDetailSaveResult.Success();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to save income detail changes.");
            return IncomeDetailSaveResult.Failure(FluxoLogManager.CreateFailureMessage("save income"));
        }
        finally
        {
            IsSaving = false;
        }
    }

    public bool HasValidChangesToPersistOnClose()
    {
        if (!IsEditing)
            return false;

        if (!TryBuildInput(out var input, out _))
            return false;

        return GetChangedFields(input, _savedState) != IncomeDetailChangedFields.None;
    }

    private void LoadFromSavedState()
    {
        AmountText = _savedState.Amount;
        NameText = _savedState.Name;
        NoteText = _savedState.Note;
        PopupTitle = "Income Detail";
        SelectedDate = _savedState.Date == default ? DateTime.Today : _savedState.Date.Date;
        SelectedSpendingSource = SpendingSources.FirstOrDefault(source => source.Id == _savedState.SpendingSourceId) ??
                                 SpendingSources.FirstOrDefault();
    }

    private bool TryBuildInput(out IncomeDetailInput input, out string validationMessage)
    {
        input = default;
        validationMessage = string.Empty;

        if (AmountText <= 0m)
        {
            validationMessage = "Please enter a valid amount greater than zero.";
            return false;
        }

        if (SelectedSpendingSource is null)
        {
            validationMessage = "Please choose a spending source.";
            return false;
        }

        input = new IncomeDetailInput(
            NameText.Trim(),
            AmountText,
            SelectedSpendingSource.Id,
            SelectedDate.Date,
            NoteText.Trim());

        return true;
    }

    private void ReloadChoicesFromMainViewModel()
    {
        _availableSpendingSources.Clear();
        _availableSpendingSources.AddRange(_mainViewModel.BudgetPanel.SpendingSources.Where(source => source.IsEnabled));

        var currentSource = _incomeLog.SpendingSource;
        if (currentSource is not null && _availableSpendingSources.All(source => source.Id != currentSource.Id))
            _availableSpendingSources.Add(currentSource);

        RefreshSpendingSources();
    }

    private void RefreshSpendingSources()
    {
        var selectedSpendingSourceId = SelectedSpendingSource?.Id;
        ReplaceCollection(SpendingSources, _availableSpendingSources
            .OrderBy(source => source.SpendingSourceType)
            .ThenBy(source => source.Name));

        SelectedSpendingSource = selectedSpendingSourceId is null
            ? SpendingSources.FirstOrDefault()
            : SpendingSources.FirstOrDefault(source => source.Id == selectedSpendingSourceId.Value) ??
              SpendingSources.FirstOrDefault();
    }

    private static string BuildIncomeName(string name, string note)
    {
        if (!string.IsNullOrWhiteSpace(name))
            return name.Trim();

        if (string.IsNullOrWhiteSpace(note))
            return "Income";

        var firstMeaningfulLine = note
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

        return string.IsNullOrWhiteSpace(firstMeaningfulLine)
            ? "Income"
            : firstMeaningfulLine;
    }

    private static void ApplyIncomeToSpendingSource(SpendingSource spendingSource, decimal amount)
    {
        if (spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            spendingSource.SpentAmount = Math.Max(0m, spendingSource.SpentAmount - amount);
            return;
        }

        spendingSource.Balance += amount;
    }

    private static void RevertIncomeFromSpendingSource(SpendingSource spendingSource, decimal amount)
    {
        if (spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            spendingSource.SpentAmount += amount;
            return;
        }

        spendingSource.Balance -= amount;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();

        foreach (var item in items)
            target.Add(item);
    }

    private static IncomeDetailSavedState CreateSavedState(IncomeLogVM incomeLog)
    {
        return new IncomeDetailSavedState(
            incomeLog.Name?.Trim() ?? string.Empty,
            incomeLog.Amount,
            incomeLog.Notes?.Trim() ?? string.Empty,
            incomeLog.AddedOn == default ? DateTime.Today : incomeLog.AddedOn.Date,
            incomeLog.SpendingSource?.Id ?? 0);
    }

    private static IncomeDetailChangedFields GetChangedFields(IncomeDetailInput input,
        IncomeDetailSavedState savedState)
    {
        var changedFields = IncomeDetailChangedFields.None;

        if (!string.Equals(input.Name, savedState.Name, StringComparison.Ordinal))
            changedFields |= IncomeDetailChangedFields.Name;

        if (input.Amount != savedState.Amount)
            changedFields |= IncomeDetailChangedFields.Amount;

        if (input.Date.Date != savedState.Date.Date)
            changedFields |= IncomeDetailChangedFields.Date;

        if (input.SpendingSourceId != savedState.SpendingSourceId)
            changedFields |= IncomeDetailChangedFields.SpendingSource;

        if (!string.Equals(input.Note, savedState.Note, StringComparison.Ordinal))
            changedFields |= IncomeDetailChangedFields.Note;

        return changedFields;
    }

    public readonly record struct IncomeDetailSaveResult(bool IsSuccess, string? ErrorMessage)
    {
        public static IncomeDetailSaveResult Success()
        {
            return new IncomeDetailSaveResult(true, null);
        }

        public static IncomeDetailSaveResult Failure(string? errorMessage)
        {
            return new IncomeDetailSaveResult(false, errorMessage);
        }
    }

    private readonly record struct IncomeDetailInput(
        string Name,
        decimal Amount,
        int SpendingSourceId,
        DateTime Date,
        string Note);

    private readonly record struct IncomeDetailSavedState(
        string Name,
        decimal Amount,
        string Note,
        DateTime Date,
        int SpendingSourceId);

    [Flags]
    private enum IncomeDetailChangedFields
    {
        None = 0,
        Name = 1,
        Amount = 2,
        Date = 4,
        SpendingSource = 8,
        Note = 16
    }
}
