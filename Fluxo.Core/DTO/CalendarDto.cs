using Fluxo.Core.Enums;

namespace Fluxo.Core.DTO;

public sealed record CalendarDto(
    DateOnly Date,
    decimal TotalSpent,
    decimal TotalEarned,
    IReadOnlyList<CalendarExpenseItem> Expenses,
    IReadOnlyList<CalendarIncomeItem> Incomes,
    IReadOnlyList<CalendarGoalDeadlineItem> GoalDeadlines,
    IReadOnlyList<CalendarRecurringTransactionItem> RecurringTransactions)
{
    public int GoalsDue => GoalDeadlines.Count;
    public int PaymentsDue => RecurringTransactions.Count;
}

public sealed record CalendarExpenseItem(
    int Id,
    string Name,
    decimal Amount,
    string AccountName,
    string? TagName);

public sealed record CalendarIncomeItem(
    int Id,
    string Name,
    decimal Amount,
    string AccountName);

public sealed record CalendarGoalDeadlineItem(
    int Id,
    string Name,
    decimal CurrentAmount,
    decimal TargetAmount,
    DateTime SavingEndDate);

public sealed record CalendarRecurringTransactionItem(
    int Id,
    string Name,
    decimal Amount,
    RecurringTransactionType Type,
    RecurringPeriod RecurringPeriod,
    int RecurringTime,
    string SourceName);
