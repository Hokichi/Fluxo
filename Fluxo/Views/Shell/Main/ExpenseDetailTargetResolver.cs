using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Entities;

namespace Fluxo.Views.Shell.Main;

internal static class ExpenseDetailTargetResolver
{
    public static async Task<ExpenseLogVM> ResolveAsync(
        ExpenseLogVM expenseLog,
        IAppDataService appData,
        CancellationToken cancellationToken = default)
    {
        if (expenseLog.ParentLogId is not { } parentLogId)
            return expenseLog;

        var parentLog = await appData.GetExpenseLogByLogIdAsync(parentLogId, cancellationToken);
        return parentLog is null ? expenseLog : ToViewModel(parentLog);
    }

    private static ExpenseLogVM ToViewModel(ExpenseLog log)
    {
        return new ExpenseLogVM
        {
            Id = log.Id,
            Amount = log.Amount,
            DeductedOn = log.DeductedOn,
            Notes = log.Notes,
            IsForDeletion = log.IsForDeletion,
            IsPinned = log.IsPinned,
            IsLend = log.IsLend,
            ParentLogId = log.ParentLogId,
            Account = new AccountVM
            {
                Id = log.Account?.Id ?? log.AccountId,
                Name = log.Account?.Name ?? string.Empty,
                AccountType = log.Account?.AccountType ?? default,
                AccountLimit = log.Account?.AccountLimit ?? 0m,
                MaximumSpending = log.Account?.MaximumSpending ?? 0m,
                MinimumPayment = log.Account?.MinimumPayment,
                SpentAmount = log.Account?.SpentAmount ?? 0m,
                Balance = log.Account?.Balance ?? 0m,
                MonthlyDueDate = log.Account?.MonthlyDueDate,
                DeductSource = log.Account?.DeductSource,
                InterestRate = log.Account?.InterestRate,
                PinnedOnUI = log.Account?.PinnedOnUI ?? false,
                IsEnabled = log.Account?.IsEnabled ?? false
            },
            Expense = new ExpenseVM
            {
                Id = log.Expense?.Id ?? log.ExpenseId,
                Name = log.Expense?.Name ?? string.Empty,
                Amount = log.Expense?.Amount ?? log.Amount,
                ExpenseCategory = log.Expense?.ExpenseCategory ?? default,
                IsLend = log.Expense?.IsLend ?? false,
                Tag = new TagVM
                {
                    Id = log.Expense?.Tag?.Id ?? log.Expense?.TagId ?? 0,
                    Name = log.Expense?.Tag?.Name ?? string.Empty,
                    HexCode = log.Expense?.Tag?.HexCode ?? string.Empty,
                    IsSystemTag = log.Expense?.Tag?.IsSystemTag ?? false,
                    SpendingLimit = log.Expense?.Tag?.SpendingLimit
                }
            }
        };
    }
}
