using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Shell.Main;

internal static class BudgetEffectiveExpenseLogFilter
{
    public static List<ExpenseLogVM> SelectBudgetEffectiveLogs(IEnumerable<ExpenseLogVM> expenseLogs)
    {
        var activeLogs = expenseLogs
            .Where(log => !log.IsForDeletion && !log.IsExcludedFromBudget)
            .ToList();
        var parentLogIds = BuildParentLogIds(activeLogs);

        return activeLogs
            .Where(log => IsBudgetEffectiveLog(log, parentLogIds))
            .ToList();
    }

    public static bool IsBudgetEffectiveLog(ExpenseLogVM expenseLog, IReadOnlySet<int> parentLogIds)
    {
        return !expenseLog.IsForDeletion &&
               !expenseLog.IsExcludedFromBudget &&
               !parentLogIds.Contains(expenseLog.Id);
    }

    public static HashSet<int> BuildParentLogIds(IEnumerable<ExpenseLogVM> expenseLogs)
    {
        return expenseLogs
            .Where(log => !log.IsForDeletion && !log.IsExcludedFromBudget)
            .Select(log => log.ParentLogId)
            .Where(parentLogId => parentLogId is > 0)
            .Select(parentLogId => parentLogId!.Value)
            .ToHashSet();
    }
}
