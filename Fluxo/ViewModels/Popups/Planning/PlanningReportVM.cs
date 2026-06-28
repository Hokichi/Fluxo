using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Budgeting;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Popups.Planning;

public sealed partial class PlanningReportVM : ObservableObject, IDisposable
{
    private readonly BudgetAllocationBalancer _allocationBalancer = new();
    private readonly IAppDataService _appData;
    private readonly List<TransactionVM> _trackedExpenses = [];
    private readonly List<TransactionVM> _trackedIncomes = [];
    private bool _disposed;
    private bool _isApplyingAllocation;
    private int _investPercent;
    private int _needsPercent;
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

    public ObservableCollection<TransactionVM> Incomes { get; }

    public ObservableCollection<TransactionVM> Expenses { get; }

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

    public void AddIncome(TransactionVM income)
    {
        Incomes.Add(CopyIncome(income));
    }

    public void AddExpense(TransactionVM expense)
    {
        Expenses.Add(CopyExpense(expense));
    }

    public async Task LoadRecurringIncomesAsync(CancellationToken cancellationToken = default)
    {
        var recurringTransactions = await _appData.GetRecurringTransactionsAsync(cancellationToken);
        foreach (var recurring in recurringTransactions.Where(transaction =>
                     transaction.IsEnabled && transaction.Type == RecurringTransactionType.Income))
        {
            if (Incomes.Any(income => income.Id == recurring.Id))
                continue;

            AddIncome(new TransactionVM
            {
                Id = recurring.Id,
                Name = recurring.Name,
                Amount = recurring.Amount,
                OccurredOn = DateTime.Now,
                Account = CopyAccount(recurring.Source, recurring.SourceId)
            });
        }
    }

    public async Task LoadRecurringExpensesAsync(CancellationToken cancellationToken = default)
    {
        var recurringTransactions = await _appData.GetRecurringTransactionsAsync(cancellationToken);
        foreach (var recurring in recurringTransactions.Where(transaction =>
                     transaction.IsEnabled && transaction.Type == RecurringTransactionType.Expense))
        {
            if (Expenses.Any(expense => expense.Id == recurring.Id))
                continue;

            AddExpense(new TransactionVM
            {
                Id = recurring.Id,
                Name = recurring.Name,
                Amount = recurring.Amount,
                ExpenseCategory = recurring.Category ?? ExpenseCategory.Needs,
                Tag = CopyTag(recurring.Tag, recurring.TagId),
                Account = CopyAccount(recurring.Source, recurring.SourceId)
            });
        }
    }

    public bool RemoveIncome(TransactionVM income)
    {
        if (Incomes.Remove(income))
            return true;

        var existing = Incomes.FirstOrDefault(x => x.Id == income.Id);
        return existing is not null && Incomes.Remove(existing);
    }

    public bool RemoveExpense(TransactionVM expense)
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

        var balanced = _allocationBalancer.Balance(NeedsPercent, WantsPercent, InvestPercent, segment, value);
        ApplyAllocationPercentages(balanced.Needs, balanced.Wants, balanced.Invest);
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
        if (e.PropertyName == nameof(TransactionVM.Amount))
            RecalculateTotals();
    }

    private void OnExpenseChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TransactionVM.Amount) ||
            e.PropertyName == nameof(TransactionVM.ExpenseCategory))
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

    private static TransactionVM CopyIncome(TransactionVM source)
    {
        return new TransactionVM
        {
            Id = source.Id,
            Name = source.Name,
            Amount = source.Amount,
            OccurredOn = source.OccurredOn,
            Notes = source.Notes,
            Account = CopyAccount(source.Account)
        };
    }

    private static TransactionVM CopyExpense(TransactionVM source)
    {
        return new TransactionVM
        {
            Id = source.Id,
            Name = source.Name,
            Amount = source.Amount,
            ExpenseCategory = source.ExpenseCategory,
            Tag = CopyTag(source.Tag),
            Account = CopyAccount(source.Account)
        };
    }

    private static TagVM CopyTag(TagVM source)
    {
        return new TagVM
        {
            Id = source.Id,
            Name = source.Name,
            HexCode = source.HexCode,
            IsSystemTag = source.IsSystemTag,
            SpendingLimit = source.SpendingLimit
        };
    }

    private static TagVM CopyTag(Tag? source, int? fallbackId)
    {
        if (source is null)
            return new TagVM { Id = fallbackId ?? 0 };

        return new TagVM
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

    private static AccountVM CopyAccount(Account? source, int fallbackId)
    {
        if (source is null)
            return new AccountVM { Id = fallbackId };

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
            IsEnabled = source.IsEnabled
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
