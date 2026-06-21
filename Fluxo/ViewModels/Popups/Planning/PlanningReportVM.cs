using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Popups.Planning;

public sealed partial class PlanningReportVM : ObservableObject, IDisposable
{
    private readonly IAppDataService _appData;
    private readonly List<ExpenseVM> _trackedExpenses = [];
    private readonly List<IncomeLogVM> _trackedIncomes = [];
    private bool _disposed;
    private bool _isApplyingAllocation;
    private int _investPercent;
    private int _needsPercent;
    private BudgetAllocationSegment? _lastRedistributionSegment;
    private bool _lastRedistributionIncreasedOtherBuckets;
    private bool _nextOddRemainderUsesPrimaryBucket = true;
    private int _wantsPercent;

    [ObservableProperty] private decimal _balance;
    [ObservableProperty] private decimal _totalExpenses;
    [ObservableProperty] private decimal _totalIncome;
    [ObservableProperty] private double _needsUsage;
    [ObservableProperty] private double _wantsUsage;
    [ObservableProperty] private double _investUsage;
    [ObservableProperty] private double _needsOverflow;
    [ObservableProperty] private double _wantsOverflow;
    [ObservableProperty] private double _investOverflow;

    [ObservableProperty] private int _needsUsagePercent;
    [ObservableProperty] private int _wantsUsagePercent;
    [ObservableProperty] private int _investUsagePercent;

    [ObservableProperty] private decimal _needsAllocated;
    [ObservableProperty] private decimal _wantsAllocated;
    [ObservableProperty] private decimal _investAllocated;

    [ObservableProperty] private decimal _needsSpent;
    [ObservableProperty] private decimal _wantsSpent;
    [ObservableProperty] private decimal _investSpent;

    public PlanningReportVM(IAppDataService appData)
    {
        _appData = appData;
        Incomes = new();
        Expenses = new();

        Incomes.CollectionChanged += OnIncomesChanged;
        Expenses.CollectionChanged += OnExpensesChanged;

        RebuildIncomeSubscriptions();
        RebuildExpenseSubscriptions();
        RecalculateTotals();
    }

    public ObservableCollection<IncomeLogVM> Incomes { get; }

    public ObservableCollection<ExpenseVM> Expenses { get; }

    public int NeedsPercent
    {
        get => _needsPercent;
        set => SetAllocation(BudgetAllocationSegment.Needs, value);
    }

    public int WantsPercent
    {
        get => _wantsPercent;
        set => SetAllocation(BudgetAllocationSegment.Wants, value);
    }

    public int InvestPercent
    {
        get => _investPercent;
        set => SetAllocation(BudgetAllocationSegment.Invest, value);
    }

    public bool IsAllocationInvalid => NeedsPercent == 0 || WantsPercent == 0 || InvestPercent == 0;

    public string InvalidAllocationMessage
    {
        get
        {
            var zeroBuckets = new List<string>(3);
            if (NeedsPercent == 0)
                zeroBuckets.Add("Needs");
            if (WantsPercent == 0)
                zeroBuckets.Add("Wants");
            if (InvestPercent == 0)
                zeroBuckets.Add("Invest");

            return zeroBuckets.Count switch
            {
                0 => string.Empty,
                1 => $"Invalid allocation.\n{zeroBuckets[0]} cannot be 0%",
                2 => $"Invalid allocation.\n{zeroBuckets[0]} and {zeroBuckets[1]} cannot be 0%",
                _ => "Invalid allocation.\nNeeds, Wants and Invest cannot be 0%"
            };
        }
    }

    public double NeedsRatio => NeedsPercent / 100d;

    public double WantsRatio => WantsPercent / 100d;

    public double InvestRatio => InvestPercent / 100d;

    public double WantsArcRotationDegrees => NeedsPercent * 3.6d;

    public double InvestArcRotationDegrees => (NeedsPercent + WantsPercent) * 3.6d;

    public async Task LoadAsync()
    {
        var allocation = await _appData.GetBudgetAllocationAsync();
        ApplyAllocationPercentages(
            Math.Clamp(allocation.NeedsThreshold, 0, 100),
            Math.Clamp(allocation.WantsThreshold, 0, 100),
            Math.Clamp(allocation.InvestThreshold, 0, 100));
    }

    public void AddIncome(IncomeLogVM income)
    {
        Incomes.Add(CopyIncome(income));
    }

    public void AddExpense(ExpenseVM expense)
    {
        Expenses.Add(CopyExpense(expense));
    }

    public bool RemoveIncome(IncomeLogVM income)
    {
        if (Incomes.Remove(income))
            return true;

        var existing = Incomes.FirstOrDefault(x => x.Id == income.Id);
        return existing is not null && Incomes.Remove(existing);
    }

    public bool RemoveExpense(ExpenseVM expense)
    {
        if (Expenses.Remove(expense))
            return true;

        var existing = Expenses.FirstOrDefault(x => x.Id == expense.Id);
        return existing is not null && Expenses.Remove(existing);
    }

    private void SetAllocation(BudgetAllocationSegment segment, int value)
    {
        if (_isApplyingAllocation)
            return;

        var requestedValue = Math.Clamp(value, 0, 100);
        var oldValue = GetAllocation(segment);
        if (oldValue == requestedValue)
            return;

        var values = new Dictionary<BudgetAllocationSegment, int>
        {
            [BudgetAllocationSegment.Needs] = NeedsPercent,
            [BudgetAllocationSegment.Wants] = WantsPercent,
            [BudgetAllocationSegment.Invest] = InvestPercent
        };

        values[segment] = requestedValue;
        var delta = requestedValue - oldValue;
        var increaseOtherBuckets = delta < 0;
        ResetOddRemainderSequenceIfNeeded(segment, increaseOtherBuckets);
        RedistributeDelta(values, segment, delta, _nextOddRemainderUsesPrimaryBucket);
        if (Math.Abs(delta) % 2 == 1)
            _nextOddRemainderUsesPrimaryBucket = !_nextOddRemainderUsesPrimaryBucket;

        ApplyAllocationPercentages(
            values[BudgetAllocationSegment.Needs],
            values[BudgetAllocationSegment.Wants],
            values[BudgetAllocationSegment.Invest]);
    }

    private int GetAllocation(BudgetAllocationSegment segment) => segment switch
    {
        BudgetAllocationSegment.Needs => NeedsPercent,
        BudgetAllocationSegment.Wants => WantsPercent,
        BudgetAllocationSegment.Invest => InvestPercent,
        _ => 0
    };

    private static void RedistributeDelta(
        IDictionary<BudgetAllocationSegment, int> values,
        BudgetAllocationSegment changedSegment,
        int delta,
        bool oddRemainderUsesPrimaryBucket)
    {
        if (delta == 0)
            return;

        var increaseOtherBuckets = delta < 0;
        var remaining = Math.Abs(delta);
        var distributionOrder = GetDistributionOrder(changedSegment, increaseOtherBuckets);
        var primaryShare = remaining / 2;
        var secondaryShare = remaining / 2;
        if (remaining % 2 == 1)
        {
            if (oddRemainderUsesPrimaryBucket)
                primaryShare++;
            else
                secondaryShare++;
        }

        var shares = new[] { primaryShare, secondaryShare };

        for (var index = 0; index < distributionOrder.Count; index++)
            remaining -= ApplyShare(values, distributionOrder[index], shares[index], increaseOtherBuckets);

        var spillIndex = 0;
        while (remaining > 0 && spillIndex < distributionOrder.Count)
        {
            var applied = ApplyShare(values, distributionOrder[spillIndex], remaining, increaseOtherBuckets);
            remaining -= applied;
            spillIndex = applied == 0 ? spillIndex + 1 : spillIndex;
        }
    }

    private static int ApplyShare(
        IDictionary<BudgetAllocationSegment, int> values,
        BudgetAllocationSegment segment,
        int requestedShare,
        bool increase)
    {
        if (requestedShare <= 0)
            return 0;

        var currentValue = values[segment];
        var capacity = increase ? 100 - currentValue : currentValue;
        var applied = Math.Min(requestedShare, capacity);
        values[segment] = increase ? currentValue + applied : currentValue - applied;
        return applied;
    }

    private static IReadOnlyList<BudgetAllocationSegment> GetDistributionOrder(
        BudgetAllocationSegment changedSegment,
        bool increase)
    {
        var ordered = new[]
        {
            BudgetAllocationSegment.Needs,
            BudgetAllocationSegment.Wants,
            BudgetAllocationSegment.Invest
        }.Where(segment => segment != changedSegment);

        return (increase
                ? ordered.OrderBy(GetPriority)
                : ordered.OrderByDescending(GetPriority))
            .ToList();
    }

    private static int GetPriority(BudgetAllocationSegment segment) => segment switch
    {
        BudgetAllocationSegment.Needs => 0,
        BudgetAllocationSegment.Wants => 1,
        BudgetAllocationSegment.Invest => 2,
        _ => 3
    };

    private void ResetOddRemainderSequenceIfNeeded(
        BudgetAllocationSegment segment,
        bool increaseOtherBuckets)
    {
        if (_lastRedistributionSegment == segment &&
            _lastRedistributionIncreasedOtherBuckets == increaseOtherBuckets)
        {
            return;
        }

        _lastRedistributionSegment = segment;
        _lastRedistributionIncreasedOtherBuckets = increaseOtherBuckets;
        _nextOddRemainderUsesPrimaryBucket = true;
    }

    private void ApplyAllocationPercentages(int needs, int wants, int invest)
    {
        _isApplyingAllocation = true;
        try
        {
            SetProperty(ref _needsPercent, needs, nameof(NeedsPercent));
            SetProperty(ref _wantsPercent, wants, nameof(WantsPercent));
            SetProperty(ref _investPercent, invest, nameof(InvestPercent));
        }
        finally
        {
            _isApplyingAllocation = false;
        }

        OnAllocationPercentagesChanged();
    }

    private void OnAllocationPercentagesChanged()
    {
        OnPropertyChanged(nameof(NeedsRatio));
        OnPropertyChanged(nameof(WantsRatio));
        OnPropertyChanged(nameof(InvestRatio));
        OnPropertyChanged(nameof(WantsArcRotationDegrees));
        OnPropertyChanged(nameof(InvestArcRotationDegrees));
        OnPropertyChanged(nameof(IsAllocationInvalid));
        OnPropertyChanged(nameof(InvalidAllocationMessage));
        RecalculateAllocationUsage();
    }

    private void OnIncomesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildIncomeSubscriptions();
        RecalculateTotals();
    }

    private void OnExpensesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildExpenseSubscriptions();
        RecalculateTotals();
    }

    private void RebuildIncomeSubscriptions()
    {
        foreach (var income in _trackedIncomes)
            income.PropertyChanged -= OnIncomeChanged;

        _trackedIncomes.Clear();

        foreach (var income in Incomes)
        {
            income.PropertyChanged += OnIncomeChanged;
            _trackedIncomes.Add(income);
        }
    }

    private void RebuildExpenseSubscriptions()
    {
        foreach (var expense in _trackedExpenses)
            expense.PropertyChanged -= OnExpenseChanged;

        _trackedExpenses.Clear();

        foreach (var expense in Expenses)
        {
            expense.PropertyChanged += OnExpenseChanged;
            _trackedExpenses.Add(expense);
        }
    }

    private void OnIncomeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IncomeLogVM.Amount))
            RecalculateTotals();
    }

    private void OnExpenseChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExpenseVM.Amount) ||
            e.PropertyName == nameof(ExpenseVM.ExpenseCategory))
            RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        TotalIncome = Incomes.Sum(income => income.Amount);
        TotalExpenses = Expenses.Sum(expense => expense.Amount);
        Balance = TotalIncome - TotalExpenses;
        RecalculateAllocationUsage();
    }

    private void RecalculateAllocationUsage()
    {
        NeedsAllocated = decimal.Round(TotalIncome * NeedsPercent / 100m, 2);
        WantsAllocated = decimal.Round(TotalIncome * WantsPercent / 100m, 2);
        InvestAllocated = decimal.Round(TotalIncome * InvestPercent / 100m, 2);

        NeedsSpent = SumByCategory(ExpenseCategory.Needs);
        WantsSpent = SumByCategory(ExpenseCategory.Wants);
        InvestSpent = SumByCategory(ExpenseCategory.Savings);

        ComputeUsageMetrics(
            NeedsSpent,
            NeedsAllocated,
            out var needsUsage,
            out var needsOverflow,
            out var needsUsagePercent);
        ComputeUsageMetrics(
            WantsSpent,
            WantsAllocated,
            out var wantsUsage,
            out var wantsOverflow,
            out var wantsUsagePercent);
        ComputeUsageMetrics(
            InvestSpent,
            InvestAllocated,
            out var investUsage,
            out var investOverflow,
            out var investUsagePercent);

        NeedsUsage = needsUsage;
        WantsUsage = wantsUsage;
        InvestUsage = investUsage;
        NeedsOverflow = needsOverflow;
        WantsOverflow = wantsOverflow;
        InvestOverflow = investOverflow;
        NeedsUsagePercent = needsUsagePercent;
        WantsUsagePercent = wantsUsagePercent;
        InvestUsagePercent = investUsagePercent;
    }

    private decimal SumByCategory(ExpenseCategory category)
    {
        return Expenses
            .Where(expense => expense.ExpenseCategory == category)
            .Sum(expense => expense.Amount);
    }

    private static void ComputeUsageMetrics(
        decimal spent,
        decimal allocation,
        out double usage,
        out double overflow,
        out int usagePercent)
    {
        var totalPercentage = allocation <= 0m
            ? 0d
            : (double)(spent / allocation);

        if (double.IsNaN(totalPercentage) || double.IsInfinity(totalPercentage))
            totalPercentage = 0d;

        totalPercentage = Math.Max(totalPercentage, 0d);
        usage = Math.Clamp(totalPercentage, 0d, 1d);
        overflow = Math.Max(0d, totalPercentage - 1d);
        usagePercent = (int)Math.Round(totalPercentage * 100d, MidpointRounding.AwayFromZero);
    }

    private static IncomeLogVM CopyIncome(IncomeLogVM source)
    {
        return new IncomeLogVM
        {
            Id = source.Id,
            Name = source.Name,
            Amount = source.Amount,
            AddedOn = source.AddedOn,
            Notes = source.Notes,
            Account = CopyAccount(source.Account)
        };
    }

    private static ExpenseVM CopyExpense(ExpenseVM source)
    {
        return new ExpenseVM
        {
            Id = source.Id,
            Name = source.Name,
            Amount = source.Amount,
            ExpenseCategory = source.ExpenseCategory,
            ExpenseTag = CopyExpenseTag(source.ExpenseTag),
            Account = CopyAccount(source.Account)
        };
    }

    private static ExpenseTagVM CopyExpenseTag(ExpenseTagVM source)
    {
        return new ExpenseTagVM
        {
            Id = source.Id,
            Name = source.Name,
            HexCode = source.HexCode,
            IsSystemTag = source.IsSystemTag,
            SpendingLimit = source.SpendingLimit
        };
    }

    private static AccountVM CopyAccount(AccountVM source)
    {
        return new AccountVM
        {
            Id = source.Id,
            Name = source.Name,
            AccountType = source.AccountType,
            AccountLimit = source.AccountLimit,
            MaximumSpending = source.MaximumSpending,
            MinimumPayment = source.MinimumPayment,
            SpentAmount = source.SpentAmount,
            Balance = source.Balance,
            MonthlyDueDate = source.MonthlyDueDate,
            DeductSource = source.DeductSource,
            InterestRate = source.InterestRate,
            PinnedOnUI = source.PinnedOnUI,
            IsEnabled = source.IsEnabled,
            MoneyIn = source.MoneyIn,
            MoneyOut = source.MoneyOut,
            IsSelected = source.IsSelected
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Incomes.CollectionChanged -= OnIncomesChanged;
        Expenses.CollectionChanged -= OnExpensesChanged;

        foreach (var income in _trackedIncomes)
            income.PropertyChanged -= OnIncomeChanged;

        foreach (var expense in _trackedExpenses)
            expense.PropertyChanged -= OnExpenseChanged;

        _trackedIncomes.Clear();
        _trackedExpenses.Clear();
        _disposed = true;
    }
}
