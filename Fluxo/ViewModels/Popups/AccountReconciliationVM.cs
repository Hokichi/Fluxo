using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.Services.Logging;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups.Helpers;

namespace Fluxo.ViewModels.Popups;

public partial class AccountReconciliationVM : ObservableObject
{
    private readonly IAppDataService _appData;
    private readonly Func<Task> _reloadCurrentDataAsync;

    [ObservableProperty] private decimal _amountText;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private AccountVM? _selectedAccount;

    public AccountReconciliationVM(
        IEnumerable<AccountVM> accounts,
        AccountVM promptSource,
        IAppDataService appData,
        Func<Task> reloadCurrentDataAsync)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(promptSource);
        ArgumentNullException.ThrowIfNull(appData);
        ArgumentNullException.ThrowIfNull(reloadCurrentDataAsync);

        _appData = appData;
        _reloadCurrentDataAsync = reloadCurrentDataAsync;

        AccountsView = AccountComboBoxViewFactory.CreateGroupedByTypeThenName(
            Accounts,
            nameof(AccountVM.TypeDisplayName),
            nameof(AccountVM.AccountType),
            nameof(AccountVM.Name));

        foreach (var source in accounts
                     .Where(CanReconcile)
                     .OrderBy(source => source.AccountType)
                     .ThenBy(source => source.Name))
        {
            Accounts.Add(source);
        }

        SelectedAccount = Accounts.FirstOrDefault(source => source.Id == promptSource.Id) ??
                                 Accounts.FirstOrDefault();
    }

    public ObservableCollection<AccountVM> Accounts { get; } = [];

    public ICollectionView AccountsView { get; }

    public bool CanSave => !IsSaving && AmountText > 0m && SelectedAccount is not null;

    public static bool CanReconcile(AccountVM source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.AccountType != AccountType.Saving;
    }

    public async Task<AccountReconciliationSaveResult> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (IsSaving)
            return AccountReconciliationSaveResult.Failure("This reconciliation is already being saved.");

        if (!TryBuildInput(out var input, out var validationMessage))
            return AccountReconciliationSaveResult.Failure(validationMessage);

        IsSaving = true;

        try
        {
            var account = await _appData.GetAccountByIdAsync(
                input.AccountId,
                cancellationToken);
            if (account is null)
                return AccountReconciliationSaveResult.Failure("Please choose a valid account.");

            var reconciliationTag = await EnsureBudgetReconciliationTagAsync(cancellationToken);
            var expenseName = $"{account.Name} - {SystemExpenseTags.BudgetReconciliationName}";
            var expense = new Expense
            {
                Name = expenseName,
                Amount = input.Amount,
                ExpenseCategory = ExpenseCategory.Needs,
                AccountId = account.Id,
                ExpenseTagId = reconciliationTag.Id
            };
            if (reconciliationTag.Id <= 0)
                expense.ExpenseTag = reconciliationTag;

            var expenseLog = new ExpenseLog
            {
                Expense = expense,
                AccountId = account.Id,
                Amount = input.Amount,
                DeductedOn = DateTime.Today,
                Notes = string.Empty,
                IsForDeletion = false
            };

            await _appData.AddExpenseAsync(expense, cancellationToken);
            await _appData.AddExpenseLogAsync(expenseLog, cancellationToken);
            ApplyExpenseToAccount(account, input.Amount);
            _appData.UpdateAccount(account);

            await _appData.SaveChangesAsync(cancellationToken);

            var createdExpenseLog = CreateExpenseLogViewModel(expense, expenseLog, account, reconciliationTag);
            WeakReferenceMessenger.Default.Send(new RecordLogMemoryMessage(
                new AddExpenseLogMemoryAction(new ExpenseLogMemorySnapshot(
                    expense.Id,
                    expenseLog.Id,
                    expense.Name,
                    expenseLog.Amount,
                    expense.ExpenseCategory,
                    account.Id,
                    reconciliationTag.Id,
                    expenseLog.DeductedOn,
                    expenseLog.Notes,
                    expenseLog.IsForDeletion,
                    expenseLog.ParentLogId))));
            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));
            await _reloadCurrentDataAsync();

            return AccountReconciliationSaveResult.Success(createdExpenseLog);
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to save account reconciliation.");
            return AccountReconciliationSaveResult.Failure(
                FluxoLogManager.CreateFailureMessage("save reconciliation"));
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task<ExpenseTag> EnsureBudgetReconciliationTagAsync(CancellationToken cancellationToken)
    {
        var tags = await _appData.GetExpenseTagsAsync(cancellationToken);
        var existingSystemTag = tags.FirstOrDefault(tag =>
            tag.IsSystemTag &&
            string.Equals(tag.Name, SystemExpenseTags.BudgetReconciliationName, StringComparison.OrdinalIgnoreCase));

        if (existingSystemTag is not null)
        {
            if (!string.Equals(existingSystemTag.HexCode, SystemExpenseTags.BudgetReconciliationHexCode,
                    StringComparison.Ordinal))
            {
                existingSystemTag.Name = SystemExpenseTags.BudgetReconciliationName;
                existingSystemTag.HexCode = SystemExpenseTags.BudgetReconciliationHexCode;
                existingSystemTag.IsSystemTag = true;
                _appData.UpdateExpenseTag(existingSystemTag);
            }

            return existingSystemTag;
        }

        var tag = new ExpenseTag
        {
            Name = SystemExpenseTags.BudgetReconciliationName,
            HexCode = SystemExpenseTags.BudgetReconciliationHexCode,
            IsSystemTag = true
        };
        await _appData.AddExpenseTagAsync(tag, cancellationToken);
        return tag;
    }

    private bool TryBuildInput(out AccountReconciliationInput input, out string validationMessage)
    {
        input = default;
        validationMessage = string.Empty;

        if (AmountText <= 0m)
        {
            validationMessage = "Please enter a valid reconciliation amount greater than zero.";
            return false;
        }

        if (SelectedAccount is null)
        {
            validationMessage = "Please choose a account.";
            return false;
        }

        input = new AccountReconciliationInput(AmountText, SelectedAccount.Id);
        return true;
    }

    private static ExpenseLogVM CreateExpenseLogViewModel(
        Expense expense,
        ExpenseLog expenseLog,
        Account account,
        ExpenseTag reconciliationTag)
    {
        var sourceVm = new AccountVM
        {
            Id = account.Id,
            Name = account.Name,
            AccountType = account.AccountType,
            Balance = account.Balance,
            SpentAmount = account.SpentAmount,
            IsEnabled = account.IsEnabled
        };
        var tagVm = new ExpenseTagVM
        {
            Id = reconciliationTag.Id,
            Name = reconciliationTag.Name,
            HexCode = reconciliationTag.HexCode,
            IsSystemTag = reconciliationTag.IsSystemTag,
            SpendingLimit = reconciliationTag.SpendingLimit
        };

        return new ExpenseLogVM
        {
            Id = expenseLog.Id,
            Amount = expenseLog.Amount,
            DeductedOn = expenseLog.DeductedOn,
            Notes = expenseLog.Notes,
            IsForDeletion = expenseLog.IsForDeletion,
            Account = sourceVm,
            Expense = new ExpenseVM
            {
                Id = expense.Id,
                Name = expense.Name,
                Amount = expense.Amount,
                ExpenseCategory = expense.ExpenseCategory,
                Account = sourceVm,
                ExpenseTag = tagVm
            }
        };
    }

    private static void ApplyExpenseToAccount(Account account, decimal amount)
    {
        if (account.AccountType == AccountType.Credit)
        {
            account.SpentAmount += amount;
            return;
        }

        account.Balance -= amount;
    }

    partial void OnAmountTextChanged(decimal value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnIsSavingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnSelectedAccountChanged(AccountVM? value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    public readonly record struct AccountReconciliationSaveResult(
        bool IsSuccess,
        string? ErrorMessage,
        ExpenseLogVM? CreatedExpenseLog)
    {
        public static AccountReconciliationSaveResult Success(ExpenseLogVM createdExpenseLog)
        {
            return new AccountReconciliationSaveResult(true, null, createdExpenseLog);
        }

        public static AccountReconciliationSaveResult Failure(string? errorMessage)
        {
            return new AccountReconciliationSaveResult(false, errorMessage, null);
        }
    }

    private readonly record struct AccountReconciliationInput(decimal Amount, int AccountId);
}
