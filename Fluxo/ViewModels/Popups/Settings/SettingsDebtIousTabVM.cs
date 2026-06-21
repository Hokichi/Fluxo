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

public enum DebtIouKind
{
    Lend,
    Debt
}

public sealed class DebtIouItemVM
{
    public DebtIouKind Kind { get; init; }
    public int TransactionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime Date { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public string TypeLabel => Kind == DebtIouKind.Lend ? "Lend" : "Debt";
}

public partial class SettingsDebtIousTabVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly IAppDataService _appData;
    private readonly IMessenger _messenger;
    private readonly Func<DateTime> _todayProvider;
    private readonly Func<Task> _reloadCurrentDataAsync;

    [ObservableProperty] private bool _isResolving;

    public SettingsDebtIousTabVM(
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

    public ObservableCollection<DebtIouItemVM> Items { get; } = [];

    public bool HasItems => Items.Count > 0;

    public string TotalAmountText => $"{Items.Sum(item => item.Amount).ToString("#,0.##", CultureInfo.CurrentCulture)}";

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var expenseLogs = await _appData.GetExpenseLogsAsync(cancellationToken);
        var incomeLogs = await _appData.GetIncomeLogsAsync(cancellationToken);

        var items = expenseLogs
            .Where(log => !log.IsForDeletion)
            .Where(log => log.IsLend || log.Expense?.IsLend == true)
            .Select(log => new DebtIouItemVM
            {
                Kind = DebtIouKind.Lend,
                TransactionId = log.Id,
                Name = log.Expense?.Name ?? "Lend",
                Amount = log.Amount,
                Date = log.DeductedOn,
                AccountName = log.Account?.Name ?? log.Expense?.Account?.Name ?? string.Empty
            })
            .Concat(incomeLogs
                .Where(log => !log.IsForDeletion)
                .Where(log => log.IsDebt)
                .Select(log => new DebtIouItemVM
                {
                    Kind = DebtIouKind.Debt,
                    TransactionId = log.Id,
                    Name = log.Name,
                    Amount = log.Amount,
                    Date = log.AddedOn,
                    AccountName = log.Account?.Name ?? string.Empty
                }))
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
        DebtIouItemVM item,
        CancellationToken cancellationToken = default)
    {
        if (IsResolving)
            return SettingsOperationResult.Failure("A debt or IOU is already being resolved.");

        IsResolving = true;

        try
        {
            var result = item.Kind == DebtIouKind.Lend
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
        int expenseLogId,
        CancellationToken cancellationToken)
    {
        var source = await _appData.GetExpenseLogByLogIdAsync(expenseLogId, cancellationToken);
        if (source?.Expense is null || !(source.IsLend || source.Expense.IsLend))
        {
            await LoadAsync(cancellationToken);
            return SettingsOperationResult.Success();
        }

        var account = source.Account ?? await _appData.GetAccountByIdAsync(source.AccountId, cancellationToken);
        if (account is null)
            return SettingsOperationResult.Failure("Unable to load the IOU account.");

        var beforeSnapshot = ExpenseLogMemorySnapshot.Create(source);
        var incomeLog = new IncomeLog
        {
            Name = $"{source.Expense.Name} - IOU resolved",
            Amount = source.Amount,
            AddedOn = _todayProvider().Date,
            Notes = $"Resolved lend from expense #{source.Id}",
            AccountId = account.Id,
            Account = account,
            IsDebt = false
        };

        await _appData.AddIncomeLogAsync(incomeLog, cancellationToken);
        ApplyIncomeToAccount(account, source.Amount);

        source.IsLend = false;
        source.Expense.IsLend = false;
        _appData.UpdateIncomeLog(incomeLog);
        _appData.UpdateExpense(source.Expense);
        _appData.UpdateExpenseLog(source);
        _appData.UpdateAccount(account);

        await _appData.SaveChangesAsync(cancellationToken);

        RecordResolutionActions(
            new AddIncomeLogMemoryAction(IncomeLogMemorySnapshot.Create(incomeLog)),
            new EditExpenseLogMemoryAction(beforeSnapshot, ExpenseLogMemorySnapshot.Create(source)));

        await ReloadAfterResolveAsync(cancellationToken);
        return SettingsOperationResult.Success();
    }

    private async Task<SettingsOperationResult> ResolveDebtAsync(
        int incomeLogId,
        CancellationToken cancellationToken)
    {
        var source = await _appData.GetIncomeLogByIdAsync(incomeLogId, cancellationToken);
        if (source is null || !source.IsDebt)
        {
            await LoadAsync(cancellationToken);
            return SettingsOperationResult.Success();
        }

        var account = source.Account ?? await _appData.GetAccountByIdAsync(source.AccountId, cancellationToken);
        if (account is null)
            return SettingsOperationResult.Failure("Unable to load the debt account.");

        var beforeSnapshot = IncomeLogMemorySnapshot.Create(source);
        var reconciliationTag = await EnsureBudgetReconciliationTagAsync(cancellationToken);
        var expense = new Expense
        {
            Name = $"{source.Name} - Debt resolved",
            Amount = source.Amount,
            ExpenseCategory = ExpenseCategory.Needs,
            AccountId = account.Id,
            Account = account,
            ExpenseTagId = reconciliationTag.Id,
            ExpenseTag = reconciliationTag
        };
        var expenseLog = new ExpenseLog
        {
            Expense = expense,
            Account = account,
            AccountId = account.Id,
            Amount = source.Amount,
            DeductedOn = _todayProvider().Date,
            Notes = $"Resolved debt from income #{source.Id}",
            IsForDeletion = false
        };

        await _appData.AddExpenseAsync(expense, cancellationToken);
        await _appData.AddExpenseLogAsync(expenseLog, cancellationToken);
        ApplyExpenseToAccount(account, source.Amount);

        source.IsDebt = false;
        _appData.UpdateIncomeLog(source);
        _appData.UpdateAccount(account);

        await _appData.SaveChangesAsync(cancellationToken);

        RecordResolutionActions(
            new AddExpenseLogMemoryAction(ExpenseLogMemorySnapshot.Create(expenseLog)),
            new EditIncomeLogMemoryAction(beforeSnapshot, IncomeLogMemorySnapshot.Create(source)));

        await ReloadAfterResolveAsync(cancellationToken);
        return SettingsOperationResult.Success();
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
