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
    private readonly List<AccountVM> _availableAccounts = [];

    [ObservableProperty] private decimal _amountText;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _noteText = string.Empty;
    [ObservableProperty] private string _popupTitle = "Income Detail";
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private AccountVM? _selectedAccount;

    public IncomeDetailVM(MainVM mainViewModel, IncomeLogVM incomeLog, IAppDataService appData)
    {
        _ = appData;
        _mainViewModel = mainViewModel;
        _incomeLog = incomeLog;
        AccountsView = AccountComboBoxViewFactory.CreateGroupedByTypeThenName(
            Accounts,
            nameof(AccountVM.TypeDisplayName),
            nameof(AccountVM.AccountType),
            nameof(AccountVM.Name));

        ReloadChoicesFromMainViewModel();
        LoadFromIncomeLog();
    }

    public ObservableCollection<AccountVM> Accounts { get; } = [];
    public ICollectionView AccountsView { get; }

    public AddNewTransactionVM.AddNewTransactionDraft CreateAddNewTransactionDraft()
    {
        return new AddNewTransactionVM.AddNewTransactionDraft(
            false,
            NameText,
            AmountText,
            SelectedAccount?.Id,
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
        SelectedAccount = Accounts.FirstOrDefault(source => source.Id == (_incomeLog.Account?.Id ?? 0)) ??
                                 Accounts.FirstOrDefault();
    }

    private void ReloadChoicesFromMainViewModel()
    {
        _availableAccounts.Clear();
        _availableAccounts.AddRange(_mainViewModel.BudgetPanel.Accounts.Where(source => source.IsEnabled));

        var currentSource = _incomeLog.Account;
        if (currentSource is not null && _availableAccounts.All(source => source.Id != currentSource.Id))
            _availableAccounts.Add(currentSource);

        RefreshAccounts();
    }

    private void RefreshAccounts()
    {
        var selectedAccountId = SelectedAccount?.Id;
        ReplaceCollection(Accounts, _availableAccounts
            .OrderBy(source => source.AccountType)
            .ThenBy(source => source.Name));

        SelectedAccount = selectedAccountId is null
            ? Accounts.FirstOrDefault()
            : Accounts.FirstOrDefault(source => source.Id == selectedAccountId.Value) ??
              Accounts.FirstOrDefault();
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();

        foreach (var item in items)
            target.Add(item);
    }
}
