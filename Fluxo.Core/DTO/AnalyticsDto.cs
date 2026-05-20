using Fluxo.Core.Enums;

namespace Fluxo.Core.DTO;

public sealed record AnalyticsDto(
    decimal TotalIncome,
    decimal TotalExpense,
    IReadOnlyList<AnalyticsTimeSeriesPoint> TimeSeries,
    IReadOnlyList<AnalyticsCategorySlice> CategoryRatio,
    IReadOnlyList<AnalyticsTagTotal> TopSpendingTags,
    IReadOnlyList<AnalyticsGoalItem> GoalsCreatedInPeriod);

public sealed record AnalyticsTimeSeriesPoint(
    DateOnly Period,
    decimal Income,
    decimal Expense);

public sealed record AnalyticsCategorySlice(
    ExpenseCategory Category,
    decimal Total);

public sealed record AnalyticsTagTotal(
    string TagName,
    string HexCode,
    decimal Total);

public sealed record AnalyticsGoalItem(
    int GoalId,
    string Name,
    decimal CurrentAmount,
    decimal TargetAmount,
    DateTime CreatedOn,
    DateTime? SavingEndDate);
