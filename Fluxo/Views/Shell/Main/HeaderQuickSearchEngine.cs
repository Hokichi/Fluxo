using Fluxo.ViewModels.Entities;

namespace Fluxo.Views.Shell.Main;

public enum HeaderQuickSearchResultKind
{
    Expense,
    Income
}

public sealed record HeaderQuickSearchResult(
    HeaderQuickSearchResultKind Kind,
    int Id,
    string Name,
    decimal Amount,
    DateTime Date,
    string AccountName,
    string? ExpenseTagName,
    string? ExpenseTagBrush,
    ExpenseLogVM? ExpenseLog,
    IncomeLogVM? IncomeLog)
{
    public bool IsExpense => Kind == HeaderQuickSearchResultKind.Expense;
    public bool IsIncome => Kind == HeaderQuickSearchResultKind.Income;
    public string IconResourceKey => IsIncome ? "BanknoteArrowUp" : "BanknoteArrowDown";
    public string IconBrushKey => IsIncome ? "Brush.Success" : "Brush.Danger";
}

public static class HeaderQuickSearchEngine
{
    public static IEnumerable<ExpenseLogVM> Search(IEnumerable<ExpenseLogVM> logs, string? query)
    {
        return Search(logs, [], query)
            .Where(result => result.ExpenseLog is not null)
            .Select(result => result.ExpenseLog!);
    }

    public static IEnumerable<HeaderQuickSearchResult> Search(
        IEnumerable<ExpenseLogVM> expenseLogs,
        IEnumerable<IncomeLogVM> incomeLogs,
        string? query)
    {
        ArgumentNullException.ThrowIfNull(expenseLogs);
        ArgumentNullException.ThrowIfNull(incomeLogs);

        var normalizedQuery = query?.Trim();
        if (string.IsNullOrEmpty(normalizedQuery) || normalizedQuery.Length <= 3)
            return [];

        var expenseResults = expenseLogs
            .Where(log => log.Expense?.Name?.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) == true)
            .Select(log => new HeaderQuickSearchResult(
                HeaderQuickSearchResultKind.Expense,
                log.Id,
                log.Expense?.Name?.Trim() ?? string.Empty,
                log.Amount,
                log.DeductedOn,
                log.Account?.Name?.Trim() ?? string.Empty,
                log.Expense?.ExpenseTag?.Name,
                log.Expense?.ExpenseTag?.HexCode,
                log,
                null));

        var incomeResults = incomeLogs
            .Where(log => log.Name?.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) == true)
            .Select(log => new HeaderQuickSearchResult(
                HeaderQuickSearchResultKind.Income,
                log.Id,
                log.Name?.Trim() ?? string.Empty,
                log.Amount,
                log.AddedOn,
                log.Account?.Name?.Trim() ?? string.Empty,
                null,
                null,
                null,
                log));

        return expenseResults
            .Concat(incomeResults)
            .OrderByDescending(result => result.Date)
            .Take(5);
    }
}
