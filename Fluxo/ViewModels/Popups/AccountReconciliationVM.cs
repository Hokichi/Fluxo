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
using Fluxo.Services.Notifications;
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
            var expenseName = $"{account.Name} - {SystemTags.BudgetReconciliationName}";
            var transaction = new Transaction
            {
                Type = TransactionType.Expense,
                Name = expenseName,
                Amount = input.Amount,
                OccurredOn = DateTime.Today,
                Notes = string.Empty,
                ExpenseCategory = ExpenseCategory.Needs,
                AccountId = account.Id,
                TagId = reconciliationTag.Id
            };
            if (reconciliationTag.Id <= 0)
                transaction.Tag = reconciliationTag;

            await _appData.AddTransactionAsync(transaction, cancellationToken);
            ApplyExpenseToAccount(account, input.Amount);
            _appData.UpdateAccount(account);

            await _appData.SaveChangesAsync(cancellationToken);

            var createdTransaction = CreateTransactionViewModel(transaction, account, reconciliationTag);
            WeakReferenceMessenger.Default.Send(new RecordLogMemoryMessage(
                new AddTransactionMemoryAction(TransactionMemorySnapshot.Create(transaction))));
            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));
            await _reloadCurrentDataAsync();

            FloatingNotificationPublisher.Success("Account reconciled", $"{input.Amount:N2} was reconciled against {account.Name}.", true);
            return AccountReconciliationSaveResult.Success(createdTransaction);
        }
        catch (Exception exception)
        {
            FloatingNotificationPublisher.LoggedFailure(WeakReferenceMessenger.Default, exception,
                "save reconciliation");
            return AccountReconciliationSaveResult.Failure(string.Empty);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task<Tag> EnsureBudgetReconciliationTagAsync(CancellationToken cancellationToken)
    {
        var tags = await _appData.GetTagsAsync(cancellationToken);
        var existingSystemTag = tags.FirstOrDefault(tag =>
            tag.IsSystemTag &&
            string.Equals(tag.Name, SystemTags.BudgetReconciliationName, StringComparison.OrdinalIgnoreCase));

        if (existingSystemTag is not null)
        {
            if (!string.Equals(existingSystemTag.HexCode, SystemTags.BudgetReconciliationHexCode,
                    StringComparison.Ordinal))
            {
                existingSystemTag.Name = SystemTags.BudgetReconciliationName;
                existingSystemTag.HexCode = SystemTags.BudgetReconciliationHexCode;
                existingSystemTag.IsSystemTag = true;
                _appData.UpdateTag(existingSystemTag);
            }

            return existingSystemTag;
        }

        var tag = new Tag
        {
            Name = SystemTags.BudgetReconciliationName,
            HexCode = SystemTags.BudgetReconciliationHexCode,
            IsSystemTag = true
        };
        await _appData.AddTagAsync(tag, cancellationToken);
        return tag;
    }

    private bool TryBuildInput(out AccountReconciliationInput input, out string validationMessage)
    {
        input = default;
        validationMessage = string.Empty;

        var failures = new List<string>();
        if (AmountText <= 0m)
            failures.Add("Please enter a valid reconciliation amount greater than zero.");
        if (SelectedAccount is null)
            failures.Add("Please choose a account.");
        if (failures.Count > 0)
        {
            validationMessage = string.Join(Environment.NewLine, failures);
            return false;
        }

        input = new AccountReconciliationInput(AmountText, SelectedAccount!.Id);
        return true;
    }

    private static TransactionVM CreateTransactionViewModel(
        Transaction transaction,
        Account account,
        Tag reconciliationTag)
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
        var tagVm = new TagVM
        {
            Id = reconciliationTag.Id,
            Name = reconciliationTag.Name,
            HexCode = reconciliationTag.HexCode,
            IsSystemTag = reconciliationTag.IsSystemTag,
            SpendingLimit = reconciliationTag.SpendingLimit
        };

        return new TransactionVM
        {
            Id = transaction.Id,
            Type = transaction.Type,
            Name = transaction.Name,
            Amount = transaction.Amount,
            OccurredOn = transaction.OccurredOn,
            LoggedOn = transaction.LoggedOn,
            Notes = transaction.Notes,
            IsForDeletion = transaction.IsForDeletion,
            Account = sourceVm,
            ExpenseCategory = transaction.ExpenseCategory,
            Tag = tagVm
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
        TransactionVM? CreatedTransaction)
    {
        public static AccountReconciliationSaveResult Success(TransactionVM createdTransaction)
        {
            return new AccountReconciliationSaveResult(true, null, createdTransaction);
        }

        public static AccountReconciliationSaveResult Failure(string? errorMessage)
        {
            return new AccountReconciliationSaveResult(false, errorMessage, null);
        }
    }

    private readonly record struct AccountReconciliationInput(decimal Amount, int AccountId);
}
