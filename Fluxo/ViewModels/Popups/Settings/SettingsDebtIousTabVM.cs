using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.Services.Logging;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Popups.Settings;

public enum IoUKind
{
    Lend,
    Debt
}

public sealed class IoUItemVM
{
    public IoUKind Kind { get; init; }
    public int TransactionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime Date { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public string TypeLabel => Kind == IoUKind.Lend ? "Lend" : "Debt";
}

public partial class SettingsIoUsTabVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly IAppDataService _appData;
    private readonly IMessenger _messenger;
    private readonly Func<DateTime> _todayProvider;
    private readonly Func<Task> _reloadCurrentDataAsync;

    [ObservableProperty] private bool _isResolving;

    public SettingsIoUsTabVM(
        MainVM mainViewModel,
        IAppDataService appData,
        IMessenger? messenger = null,
        Func<DateTime>? todayProvider = null,
        Func<Task>? reloadCurrentDataAsync = null)
    {
        _mainViewModel = mainViewModel;
        _appData = appData;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
        _todayProvider = todayProvider ?? (() => DateTime.Today);
        _reloadCurrentDataAsync = reloadCurrentDataAsync ?? mainViewModel.ReloadCurrentDataAsync;
    }

    public ObservableCollection<IoUItemVM> Items { get; } = [];

    public bool HasItems => Items.Count > 0;

    public string TotalAmountText => $"{Items.Sum(item => item.Amount).ToString("#,0.##", CultureInfo.CurrentCulture)}";

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var transactions = await _appData.GetTransactionsAsync(cancellationToken);
        var items = transactions
            .Where(transaction => !transaction.IsForDeletion && transaction.IsIoU)
            .Select(transaction => new IoUItemVM
            {
                Kind = transaction.Type == TransactionType.Expense ? IoUKind.Lend : IoUKind.Debt,
                TransactionId = transaction.Id,
                Name = transaction.Name,
                Amount = transaction.Amount,
                Date = transaction.OccurredOn,
                AccountName = transaction.Account?.Name ?? string.Empty
            })
            .OrderBy(item => item.Date)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Items.Clear();
        foreach (var item in items)
            Items.Add(item);

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(TotalAmountText));
    }

    public async Task<SettingsOperationResult> ResolveAsync(
        IoUItemVM item,
        CancellationToken cancellationToken = default)
    {
        if (IsResolving)
            return SettingsOperationResult.Failure("A debt or IOU is already being resolved.");

        IsResolving = true;

        try
        {
            var result = item.Kind == IoUKind.Lend
                ? await ResolveLendAsync(item.TransactionId, cancellationToken)
                : await ResolveDebtAsync(item.TransactionId, cancellationToken);

            if (!result.IsSuccess)
                return result;

            await LoadAsync(cancellationToken);
            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to resolve debt or IOU.");
            return SettingsOperationResult.Failure(
                FluxoLogManager.CreateFailureMessage("resolve debt or IOU"));
        }
        finally
        {
            IsResolving = false;
        }
    }

    private async Task<SettingsOperationResult> ResolveLendAsync(
        int transactionId,
        CancellationToken cancellationToken)
    {
        var source = await _appData.GetTransactionByIdAsync(transactionId, cancellationToken);
        if (source is null || source.Type != TransactionType.Expense || !source.IsIoU)
        {
            await LoadAsync(cancellationToken);
            return SettingsOperationResult.Success();
        }

        var account = source.Account ?? await _appData.GetAccountByIdAsync(source.AccountId, cancellationToken);
        if (account is null)
            return SettingsOperationResult.Failure("Unable to load the IOU account.");

        var beforeSnapshot = TransactionMemorySnapshot.Create(source);
        var income = new Transaction
        {
            Type = TransactionType.Income,
            Name = $"{source.Name} - IOU resolved",
            Amount = source.Amount,
            OccurredOn = _todayProvider().Date,
            Notes = $"Resolved lend from expense #{source.Id}",
            AccountId = account.Id,
            Account = account,
            IsIoU = false
        };

        await _appData.AddTransactionAsync(income, cancellationToken);
        ApplyIncomeToAccount(account, source.Amount);

        source.IsIoU = false;
        _appData.UpdateTransaction(source);
        _appData.UpdateAccount(account);

        await _appData.SaveChangesAsync(cancellationToken);

        RecordResolutionActions(
            new AddTransactionMemoryAction(TransactionMemorySnapshot.Create(income)),
            new EditTransactionMemoryAction(beforeSnapshot, TransactionMemorySnapshot.Create(source)));

        await ReloadAfterResolveAsync(cancellationToken);
        return SettingsOperationResult.Success();
    }

    private async Task<SettingsOperationResult> ResolveDebtAsync(
        int transactionId,
        CancellationToken cancellationToken)
    {
        var source = await _appData.GetTransactionByIdAsync(transactionId, cancellationToken);
        if (source is null || source.Type != TransactionType.Income || !source.IsIoU)
        {
            await LoadAsync(cancellationToken);
            return SettingsOperationResult.Success();
        }

        var account = source.Account ?? await _appData.GetAccountByIdAsync(source.AccountId, cancellationToken);
        if (account is null)
            return SettingsOperationResult.Failure("Unable to load the debt account.");

        var beforeSnapshot = TransactionMemorySnapshot.Create(source);
        var reconciliationTag = await EnsureBudgetReconciliationTagAsync(cancellationToken);
        var expense = new Transaction
        {
            Type = TransactionType.Expense,
            Name = $"{source.Name} - Debt resolved",
            Amount = source.Amount,
            OccurredOn = _todayProvider().Date,
            Notes = $"Resolved debt from income #{source.Id}",
            ExpenseCategory = ExpenseCategory.Needs,
            AccountId = account.Id,
            Account = account,
            TagId = reconciliationTag.Id,
            Tag = reconciliationTag
        };
        await _appData.AddTransactionAsync(expense, cancellationToken);
        ApplyExpenseToAccount(account, source.Amount);

        source.IsIoU = false;
        _appData.UpdateTransaction(source);
        _appData.UpdateAccount(account);

        await _appData.SaveChangesAsync(cancellationToken);

        RecordResolutionActions(
            new AddTransactionMemoryAction(TransactionMemorySnapshot.Create(expense)),
            new EditTransactionMemoryAction(beforeSnapshot, TransactionMemorySnapshot.Create(source)));

        await ReloadAfterResolveAsync(cancellationToken);
        return SettingsOperationResult.Success();
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

    private async Task ReloadAfterResolveAsync(CancellationToken cancellationToken)
    {
        _messenger.Send(new DashboardDataInvalidatedMessage(
            DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));
        await _reloadCurrentDataAsync();
    }

    private void RecordResolutionActions(params ILogMemoryAction[] actions)
    {
        _messenger.Send(new RecordLogMemoryMessage(
            new CompositeLogMemoryAction("Resolve debt or IOU", actions)));
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

    private static void ApplyIncomeToAccount(Account account, decimal amount)
    {
        if (account.AccountType == AccountType.Credit)
        {
            account.SpentAmount = Math.Max(0m, account.SpentAmount - amount);
            return;
        }

        account.Balance += amount;
    }
}
