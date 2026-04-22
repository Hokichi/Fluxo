using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.ViewModels.Shell.Main;

public enum AnalyticsTrendMode
{
    Expenses,
    Incomes,
    Both
}

public sealed partial class AnalyticsVM(IAnalyticsService analyticsService) : ObservableObject, IDisposable
{
    private const int MaxRangeDays = 31;
    private const int VerticalTrendLabelThresholdDays = 14;

    private readonly IAnalyticsService _analyticsService = analyticsService;
    private CancellationTokenSource? _refreshDebounceCts;
    private bool _isApplyingDateBounds;
    private IReadOnlyList<AnalyticsTrendPoint> _expenseTrendPoints = [];
    private IReadOnlyList<AnalyticsTrendPoint> _incomeTrendPoints = [];

    [ObservableProperty] private DateTime _startDate = DateTime.Today;
    [ObservableProperty] private DateTime _endDate = DateTime.Today;
    [ObservableProperty] private AnalyticsTrendMode _selectedTrendMode = AnalyticsTrendMode.Both;

    [ObservableProperty] private string _totalIncomeText = "0";
    [ObservableProperty] private string _totalExpenseText = "0";
    [ObservableProperty] private string _netValueText = "0";

    [ObservableProperty] private IReadOnlyList<AnalyticsTrendBarItem> _trendBarItems = [];
    [ObservableProperty] private bool _hasTrendData;

    [ObservableProperty] private double _needsRatio;
    [ObservableProperty] private double _wantsRatio;
    [ObservableProperty] private double _investRatio;
    [ObservableProperty] private double _wantsArcRotationDegrees;
    [ObservableProperty] private double _investArcRotationDegrees;
    [ObservableProperty] private int _needsPercent;
    [ObservableProperty] private int _wantsPercent;
    [ObservableProperty] private int _investPercent;
    [ObservableProperty] private bool _hasRatioData;
    [ObservableProperty] private IReadOnlyList<AnalyticsTopTagCardItem> _topSpendingTagItems = [];
    [ObservableProperty] private bool _hasTagData;

    [ObservableProperty] private IReadOnlyList<AnalyticsGoalCardItem> _goalsCreatedInPeriod = [];
    [ObservableProperty] private bool _hasGoalsData;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isTrendLabelVertical;
    [ObservableProperty] private bool _hideTrendValueLabels;
    [ObservableProperty] private string _dateRangeWarningMessage = string.Empty;

    public IReadOnlyList<AnalyticsTrendMode> TrendModes { get; } =
        Enum.GetValues<AnalyticsTrendMode>();

    public async Task LoadAsync()
    {
        await RefreshAsync(CancellationToken.None);
    }

    partial void OnStartDateChanged(DateTime value)
    {
        if (_isApplyingDateBounds)
            return;

        ApplyDateRangeRulesAndRefresh();
    }

    partial void OnEndDateChanged(DateTime value)
    {
        if (_isApplyingDateBounds)
            return;

        ApplyDateRangeRulesAndRefresh();
    }

    partial void OnSelectedTrendModeChanged(AnalyticsTrendMode value)
    {
        OnPropertyChanged(nameof(IsExpenseTrendModeSelected));
        OnPropertyChanged(nameof(IsIncomeTrendModeSelected));
        OnPropertyChanged(nameof(IsBothTrendModeSelected));
        ApplyTrendMode();
    }

    public bool IsExpenseTrendModeSelected => SelectedTrendMode == AnalyticsTrendMode.Expenses;

    public bool IsIncomeTrendModeSelected => SelectedTrendMode == AnalyticsTrendMode.Incomes;

    public bool IsBothTrendModeSelected => SelectedTrendMode == AnalyticsTrendMode.Both;

    public bool HasDateRangeWarning => !string.IsNullOrWhiteSpace(DateRangeWarningMessage);

    partial void OnDateRangeWarningMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasDateRangeWarning));
    }

    [RelayCommand]
    private void SetTrendMode(AnalyticsTrendMode trendMode)
    {
        SelectedTrendMode = trendMode;
    }

    private void QueueRefresh()
    {
        _refreshDebounceCts?.Cancel();
        _refreshDebounceCts?.Dispose();

        var cts = new CancellationTokenSource();
        _refreshDebounceCts = cts;

        _ = RefreshDebouncedAsync(cts.Token);
    }

    private async Task RefreshDebouncedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(220, cancellationToken);
            await RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;

        try
        {
            var from = DateOnly.FromDateTime(StartDate.Date);
            var to = DateOnly.FromDateTime(EndDate.Date);
            var dto = await _analyticsService.GetAnalyticsAsync(from, to, cancellationToken);
            Apply(dto);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Apply(AnalyticsDto dto)
    {
        TotalIncomeText = FormatMoney(dto.TotalIncome);
        TotalExpenseText = FormatMoney(dto.TotalExpense);
        NetValueText = FormatMoney(dto.TotalIncome - dto.TotalExpense);

        var labelFormat = dto.TimeSeries.Count <= 7 ? "ddd" : "dd MMM";
        _expenseTrendPoints = dto.TimeSeries
            .Select(point => new AnalyticsTrendPoint(
                point.Period.ToString(labelFormat, CultureInfo.InvariantCulture),
                point.Expense))
            .ToArray();
        _incomeTrendPoints = dto.TimeSeries
            .Select(point => new AnalyticsTrendPoint(
                point.Period.ToString(labelFormat, CultureInfo.InvariantCulture),
                point.Income))
            .ToArray();

        ApplyTrendMode();

        var categoryMap = dto.CategoryRatio.ToDictionary(item => item.Category, item => item.Total);
        var needs = categoryMap.GetValueOrDefault(ExpenseCategory.Needs, 0m);
        var wants = categoryMap.GetValueOrDefault(ExpenseCategory.Wants, 0m);
        var invest = categoryMap.GetValueOrDefault(ExpenseCategory.Savings, 0m);
        var ratioTotal = needs + wants + invest;
        HasRatioData = ratioTotal > 0m;

        if (ratioTotal > 0m)
        {
            NeedsRatio = (double)(needs / ratioTotal);
            WantsRatio = (double)(wants / ratioTotal);
            InvestRatio = (double)(invest / ratioTotal);
        }
        else
        {
            NeedsRatio = 0d;
            WantsRatio = 0d;
            InvestRatio = 0d;
        }

        NeedsPercent = (int)Math.Round(NeedsRatio * 100d, MidpointRounding.AwayFromZero);
        WantsPercent = (int)Math.Round(WantsRatio * 100d, MidpointRounding.AwayFromZero);
        InvestPercent = Math.Max(0, 100 - NeedsPercent - WantsPercent);
        WantsArcRotationDegrees = NeedsRatio * 360d;
        InvestArcRotationDegrees = (NeedsRatio + WantsRatio) * 360d;

        var topTags = dto.TopSpendingTags
            .OrderByDescending(item => item.Total)
            .Take(8)
            .ToArray();
        var maxTopTagTotal = topTags.Length == 0 ? 0m : topTags.Max(item => item.Total);

        TopSpendingTagItems = topTags
            .Select(item => new AnalyticsTopTagCardItem(
                item.TagName,
                item.HexCode,
                item.Total,
                maxTopTagTotal <= 0m ? 0d : Math.Clamp((double)(item.Total / maxTopTagTotal * 100m), 0d, 100d)))
            .ToArray();
        HasTagData = Enumerable.Any<AnalyticsTopTagCardItem>(TopSpendingTagItems, item => item.Total > 0m);

        GoalsCreatedInPeriod = dto.GoalsCreatedInPeriod
            .Select(goal => new AnalyticsGoalCardItem(
                goal.Name,
                goal.CurrentAmount,
                goal.TargetAmount,
                goal.CreatedOn,
                goal.SavingEndDate))
            .ToArray();
        HasGoalsData = GoalsCreatedInPeriod.Count > 0;
    }

    private void ApplyTrendMode()
    {
        IReadOnlyList<AnalyticsTrendPoint> trendPoints = SelectedTrendMode switch
        {
            AnalyticsTrendMode.Expenses => _expenseTrendPoints,
            AnalyticsTrendMode.Incomes => _incomeTrendPoints,
            _ => BuildCombinedTrendPoints()
        };

        HasTrendData = trendPoints.Any(point => point.Value > 0m);

        var isExpenseMode = SelectedTrendMode == AnalyticsTrendMode.Expenses;
        var isIncomeMode = SelectedTrendMode == AnalyticsTrendMode.Incomes;
        var maxValue = trendPoints.DefaultIfEmpty(default).Max(point => point.Value);
        TrendBarItems = trendPoints
            .Select((point, index) => new AnalyticsTrendBarItem(
                point.Label,
                point.Value,
                CalculateBarHeightRatio(point.Value, maxValue),
                IsHighlighted: index == trendPoints.Count - 1 && point.Value > 0m,
                HideValueText: HideTrendValueLabels,
                RotateLabelVertical: IsTrendLabelVertical,
                IsExpenseMode: isExpenseMode,
                IsIncomeMode: isIncomeMode))
            .ToArray();
    }

    private void ApplyDateRangeRulesAndRefresh()
    {
        _isApplyingDateBounds = true;
        try
        {
            var start = StartDate.Date;
            var end = EndDate.Date;

            if (end < start)
                end = start;

            if ((end - start).TotalDays > MaxRangeDays)
            {
                end = start.AddDays(MaxRangeDays);
                DateRangeWarningMessage =
                    "The selected range cannot exceed 31 days. End date was adjusted to 31 days after start date.";
            }
            else
            {
                DateRangeWarningMessage = string.Empty;
            }

            if (StartDate.Date != start)
                StartDate = start;
            if (EndDate.Date != end)
                EndDate = end;
        }
        finally
        {
            _isApplyingDateBounds = false;
        }

        UpdateTrendPresentation();
        ApplyTrendMode();
        QueueRefresh();
    }

    private void UpdateTrendPresentation()
    {
        var isWideRange = (EndDate.Date - StartDate.Date).TotalDays > VerticalTrendLabelThresholdDays;
        IsTrendLabelVertical = isWideRange;
        HideTrendValueLabels = isWideRange;
    }

    private IReadOnlyList<AnalyticsTrendPoint> BuildCombinedTrendPoints()
    {
        if (_expenseTrendPoints.Count == 0 || _incomeTrendPoints.Count == 0)
            return [];

        var count = Math.Min(_expenseTrendPoints.Count, _incomeTrendPoints.Count);
        return _expenseTrendPoints
            .Take(count)
            .Select((expensePoint, index) => new AnalyticsTrendPoint(
                expensePoint.Label,
                expensePoint.Value + _incomeTrendPoints[index].Value))
            .ToArray();
    }

    private static double CalculateBarHeightRatio(decimal value, decimal maxValue)
    {
        if (value <= 0m || maxValue <= 0m)
            return 0.02d;

        const double minRatio = 0.12d;
        var normalized = (double)(value / maxValue);
        return Math.Clamp(Math.Max(minRatio, normalized), 0d, 1d);
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    public void Dispose()
    {
        _refreshDebounceCts?.Cancel();
        _refreshDebounceCts?.Dispose();
        _refreshDebounceCts = null;
    }
}

public sealed record AnalyticsGoalCardItem(
    string Name,
    decimal CurrentAmount,
    decimal TargetAmount,
    DateTime CreatedOn,
    DateTime SavingEndDate)
{
    public double ProgressPercent =>
        TargetAmount <= 0m ? 0d : Math.Clamp((double)(CurrentAmount / TargetAmount * 100m), 0d, 100d);
}

public readonly record struct AnalyticsTrendPoint(string Label, decimal Value);

public sealed record AnalyticsTrendBarItem(
    string Label,
    decimal Value,
    double BarHeightRatio,
    bool IsHighlighted,
    bool HideValueText,
    bool RotateLabelVertical,
    bool IsExpenseMode,
    bool IsIncomeMode)
{
    public string ValueText => $"${Value:N0}";
}

public sealed record AnalyticsTopTagCardItem(
    string TagName,
    string HexCode,
    decimal Total,
    double ProgressPercent)
{
    public string TotalText => $"${Total:N0}";
}
