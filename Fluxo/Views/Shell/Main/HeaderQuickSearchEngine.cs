using Fluxo.ViewModels.Entities;

namespace Fluxo.Views.Shell.Main;

public static class HeaderQuickSearchEngine
{
    public static IEnumerable<ExpenseLogVM> Search(IEnumerable<ExpenseLogVM> logs, string? query)
    {
        var normalizedQuery = query?.Trim();
        if (string.IsNullOrEmpty(normalizedQuery) || normalizedQuery.Length <= 3)
        {
            return [];
        }

        return logs.Where(log =>
                log.Expense.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Take(5);
    }
}
