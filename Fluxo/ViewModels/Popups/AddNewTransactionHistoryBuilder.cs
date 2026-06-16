using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Popups;

public static class AddNewTransactionHistoryBuilder
{
    public static IReadOnlyList<AddNewTransactionHistoryItemVM> BuildPinnedExpenses(
        IEnumerable<ExpenseLog> expenseLogs,
        int pageSize = int.MaxValue)
    {
        return ProjectExpenses(expenseLogs, isPinned: true)
            .GroupBy(item => new ExpenseKey(
                NormalizeName(item.Name),
                item.Amount,
                item.SpendingSourceId,
                item.Note,
                item.Category ?? ExpenseCategory.Needs,
                item.TagId ?? 0))
            .Select(SelectNewest)
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.Id)
            .Take(pageSize)
            .ToList();
    }

    public static IReadOnlyList<AddNewTransactionHistoryItemVM> BuildExpenseHistory(
        IEnumerable<ExpenseLog> expenseLogs,
        int pageSize = int.MaxValue)
    {
        return ProjectExpenses(expenseLogs, isPinned: false)
            .GroupBy(item => new RepeatingExpenseKey(
                NormalizeName(item.Name),
                item.Amount,
                item.SpendingSourceId,
                item.TagId ?? 0))
            .Select(SelectNewest)
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.Id)
            .Take(pageSize)
            .ToList();
    }

    public static IReadOnlyList<AddNewTransactionHistoryItemVM> BuildPinnedIncomes(
        IEnumerable<IncomeLog> incomeLogs,
        int pageSize = int.MaxValue)
    {
        return ProjectIncomes(incomeLogs, isPinned: true)
            .GroupBy(item => new IncomeKey(
                NormalizeName(item.Name),
                item.Amount,
                item.SpendingSourceId,
                item.Note))
            .Select(SelectNewest)
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.Id)
            .Take(pageSize)
            .ToList();
    }

    public static IReadOnlyList<AddNewTransactionHistoryItemVM> BuildIncomeHistory(
        IEnumerable<IncomeLog> incomeLogs,
        int pageSize = int.MaxValue)
    {
        return ProjectIncomes(incomeLogs, isPinned: false)
            .GroupBy(item => new RepeatingIncomeKey(
                NormalizeName(item.Name),
                item.Amount,
                item.SpendingSourceId))
            .Select(SelectNewest)
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.Id)
            .Take(pageSize)
            .ToList();
    }

    private static IEnumerable<AddNewTransactionHistoryItemVM> ProjectExpenses(
        IEnumerable<ExpenseLog> expenseLogs,
        bool isPinned)
    {
        return expenseLogs
            .Where(log => !log.IsForDeletion)
            .Where(log => log.IsPinned == isPinned)
            .Where(log => log.Expense?.ExpenseTag?.IsSystemTag != true)
            .Where(log => !string.IsNullOrWhiteSpace(log.Expense?.Name))
            .Select(log => new AddNewTransactionHistoryItemVM
            {
                Id = log.Id,
                IsExpense = true,
                Name = log.Expense.Name,
                Amount = log.Amount,
                SpendingSourceId = log.SpendingSourceId,
                SpendingSourceName = log.SpendingSource?.Name ?? log.Expense.SpendingSource?.Name ?? string.Empty,
                Note = log.Notes ?? string.Empty,
                Date = log.DeductedOn,
                Category = log.Expense.ExpenseCategory,
                TagId = log.Expense.ExpenseTagId,
                TagHexCode = log.Expense.ExpenseTag?.HexCode,
                IsPinned = log.IsPinned
            });
    }

    private static IEnumerable<AddNewTransactionHistoryItemVM> ProjectIncomes(
        IEnumerable<IncomeLog> incomeLogs,
        bool isPinned)
    {
        return incomeLogs
            .Where(log => !log.IsForDeletion)
            .Where(log => log.IsPinned == isPinned)
            .Where(log => !string.IsNullOrWhiteSpace(log.Name))
            .Select(log => new AddNewTransactionHistoryItemVM
            {
                Id = log.Id,
                IsExpense = false,
                Name = log.Name,
                Amount = log.Amount,
                SpendingSourceId = log.SpendingSourceId,
                SpendingSourceName = log.SpendingSource?.Name ?? string.Empty,
                Note = log.Notes ?? string.Empty,
                Date = log.AddedOn,
                IsPinned = log.IsPinned
            });
    }

    private static AddNewTransactionHistoryItemVM SelectNewest(IEnumerable<AddNewTransactionHistoryItemVM> items)
    {
        return items
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.Id)
            .First();
    }

    private static string NormalizeName(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private sealed record ExpenseKey(
        string Name,
        decimal Amount,
        int SpendingSourceId,
        string Note,
        ExpenseCategory Category,
        int TagId);

    private sealed record RepeatingExpenseKey(
        string Name,
        decimal Amount,
        int SpendingSourceId,
        int TagId);

    private sealed record IncomeKey(
        string Name,
        decimal Amount,
        int SpendingSourceId,
        string Note);

    private sealed record RepeatingIncomeKey(
        string Name,
        decimal Amount,
        int SpendingSourceId);
}
