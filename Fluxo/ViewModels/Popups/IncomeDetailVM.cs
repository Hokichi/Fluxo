using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups.Helpers;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Popups;

public partial class IncomeDetailVM : ObservableObject
{
    private readonly IncomeLogVM _incomeLog;
    private readonly MainVM _mainViewModel;
    private readonly List<SpendingSourceVM> _availableSpendingSources = [];

    [ObservableProperty] private decimal _amountText;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _noteText = string.Empty;
    [ObservableProperty] private string _popupTitle = "Income Detail";
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private SpendingSourceVM? _selectedSpendingSource;

    public IncomeDetailVM(MainVM mainViewModel, IncomeLogVM incomeLog, IAppDataService appData)
    {
        _ = appData;
        _mainViewModel = mainViewModel;
        _incomeLog = incomeLog;
        SpendingSourcesView = SpendingSourceComboBoxViewFactory.CreateGroupedByTypeThenName(
            SpendingSources,
            nameof(SpendingSourceVM.TypeDisplayName),
            nameof(SpendingSourceVM.SpendingSourceType),
            nameof(SpendingSourceVM.Name));

        ReloadChoicesFromMainViewModel();
        LoadFromIncomeLog();
    }

    public ObservableCollection<SpendingSourceVM> SpendingSources { get; } = [];
    public ICollectionView SpendingSourcesView { get; }

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

    private void LoadFromIncomeLog()
    {
        AmountText = _incomeLog.Amount;
        NameText = _incomeLog.Name?.Trim() ?? string.Empty;
        NoteText = _incomeLog.Notes?.Trim() ?? string.Empty;
        PopupTitle = "Income Detail";
        SelectedDate = _incomeLog.AddedOn == default ? DateTime.Today : _incomeLog.AddedOn.Date;
        SelectedSpendingSource = SpendingSources.FirstOrDefault(source => source.Id == (_incomeLog.SpendingSource?.Id ?? 0)) ??
                                 SpendingSources.FirstOrDefault();
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

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();

        foreach (var item in items)
            target.Add(item);
    }
}
