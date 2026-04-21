using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Fluxo.ViewModels.Popups;

public enum AnalyticsTrendMode
{
    Expenses,
    Incomes,
    Both
}

public sealed partial class AnalyticsVM(IAnalyticsService analyticsService) : ObservableObject, IDisposable
{
    private readonly IAnalyticsService _analyticsService = analyticsService;
    private CancellationTokenSource? _refreshDebounceCts;
    private bool _isApplyingDateBounds;
    private ISeries[] _expenseTrendSeries = [];
    private ISeries[] _incomeTrendSeries = [];

    [ObservableProperty] private DateTime _startDate = DateTime.Today;
    [ObservableProperty] private DateTime _endDate = DateTime.Today;
    [ObservableProperty] private AnalyticsTrendMode _selectedTrendMode = AnalyticsTrendMode.Both;

    [ObservableProperty] private string _totalIncomeText = "0";
    [ObservableProperty] private string _totalExpenseText = "0";
    [ObservableProperty] private string _netValueText = "0";

    [ObservableProperty] private IEnumerable<ISeries> _trendSeries = [];
    [ObservableProperty] private Axis[] _trendXAxes = [];
    [ObservableProperty] private Axis[] _trendYAxes = [];

    [ObservableProperty] private IEnumerable<ISeries> _ratioSeries = [];
    [ObservableProperty] private IEnumerable<ISeries> _tagSeries = [];
    [ObservableProperty] private Axis[] _tagXAxes = [];
    [ObservableProperty] private Axis[] _tagYAxes = [];

    [ObservableProperty] private IReadOnlyList<AnalyticsGoalCardItem> _goalsCreatedInPeriod = [];
    [ObservableProperty] private bool _isLoading;

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

        if (value.Date > EndDate.Date)
        {
            _isApplyingDateBounds = true;
            EndDate = value.Date;
            _isApplyingDateBounds = false;
        }

        QueueRefresh();
    }

    partial void OnEndDateChanged(DateTime value)
    {
        if (_isApplyingDateBounds)
            return;

        if (value.Date < StartDate.Date)
        {
            _isApplyingDateBounds = true;
            StartDate = value.Date;
            _isApplyingDateBounds = false;
        }

        QueueRefresh();
    }

    partial void OnSelectedTrendModeChanged(AnalyticsTrendMode value)
    {
        ApplyTrendMode();
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

        var labels = dto.TimeSeries
            .Select(point => point.Period.ToString("dd MMM", CultureInfo.InvariantCulture))
            .ToArray();

        _expenseTrendSeries =
        [
            new ColumnSeries<decimal>
            {
                Name = "Expenses",
                Values = dto.TimeSeries.Select(point => point.Expense).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#FF5C5C"))
            }
        ];

        _incomeTrendSeries =
        [
            new ColumnSeries<decimal>
            {
                Name = "Incomes",
                Values = dto.TimeSeries.Select(point => point.Income).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#3FE0A1"))
            }
        ];

        TrendXAxes =
        [
            new Axis
            {
                Labels = labels,
                LabelsRotation = -25
            }
        ];

        TrendYAxes =
        [
            new Axis
            {
                Labeler = value => value.ToString("N0", CultureInfo.InvariantCulture)
            }
        ];

        ApplyTrendMode();

        var categoryMap = dto.CategoryRatio.ToDictionary(item => item.Category, item => item.Total);
        var needs = categoryMap.GetValueOrDefault(ExpenseCategory.Needs, 0m);
        var wants = categoryMap.GetValueOrDefault(ExpenseCategory.Wants, 0m);
        var invest = categoryMap.GetValueOrDefault(ExpenseCategory.Savings, 0m);

        RatioSeries =
        [
            new PieSeries<decimal>
            {
                Name = "Needs",
                Values = [needs],
                InnerRadius = 60,
                Fill = new SolidColorPaint(SKColor.Parse("#E6EAF0"))
            },
            new PieSeries<decimal>
            {
                Name = "Wants",
                Values = [wants],
                InnerRadius = 60,
                Fill = new SolidColorPaint(SKColor.Parse("#FFB86C"))
            },
            new PieSeries<decimal>
            {
                Name = "Invest",
                Values = [invest],
                InnerRadius = 60,
                Fill = new SolidColorPaint(SKColor.Parse("#3FE0A1"))
            }
        ];

        var topTags = dto.TopSpendingTags.Take(8).ToArray();
        TagSeries =
        [
            new RowSeries<decimal>
            {
                Name = "Spending",
                Values = topTags.Select(item => item.Total).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#4DA3FF"))
            }
        ];

        TagXAxes =
        [
            new Axis
            {
                Labeler = value => value.ToString("N0", CultureInfo.InvariantCulture)
            }
        ];

        TagYAxes =
        [
            new Axis
            {
                Labels = topTags.Select(item => item.TagName).ToArray()
            }
        ];

        GoalsCreatedInPeriod = dto.GoalsCreatedInPeriod
            .Select(goal => new AnalyticsGoalCardItem(
                goal.Name,
                goal.CurrentAmount,
                goal.TargetAmount,
                goal.CreatedOn,
                goal.SavingEndDate))
            .ToArray();
    }

    private void ApplyTrendMode()
    {
        TrendSeries = SelectedTrendMode switch
        {
            AnalyticsTrendMode.Expenses => _expenseTrendSeries,
            AnalyticsTrendMode.Incomes => _incomeTrendSeries,
            _ => [.. _expenseTrendSeries, .. _incomeTrendSeries]
        };
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
