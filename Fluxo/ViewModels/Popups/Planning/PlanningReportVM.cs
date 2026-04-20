using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Popups.Planning;

public sealed partial class PlanningReportVM : ObservableObject, IDisposable
{
    private readonly PlanningSnapshot _sessionSnapshot;
    private readonly List<ExpenseVM> _trackedExpenses = [];
    private readonly List<IncomeLogVM> _trackedIncomes = [];
    private bool _disposed;

    [ObservableProperty] private decimal _balance;
    [ObservableProperty] private decimal _totalExpenses;
    [ObservableProperty] private decimal _totalIncome;

    public PlanningReportVM(PlanningSnapshot snapshot)
    {
        _sessionSnapshot = snapshot;
        Incomes = new ObservableCollection<IncomeLogVM>(snapshot.Incomes.Select(PlanningSnapshot.CopyIncome));
        Expenses = new ObservableCollection<ExpenseVM>(snapshot.Expenses.Select(PlanningSnapshot.CopyExpense));
        NeedsPercent = snapshot.NeedsPercent;
        WantsPercent = snapshot.WantsPercent;
        InvestPercent = snapshot.InvestPercent;

        Incomes.CollectionChanged += OnIncomesChanged;
        Expenses.CollectionChanged += OnExpensesChanged;

        RebuildIncomeSubscriptions();
        RebuildExpenseSubscriptions();
        RecalculateTotals();
    }

    public ObservableCollection<IncomeLogVM> Incomes { get; }

    public ObservableCollection<ExpenseVM> Expenses { get; }

    public int NeedsPercent { get; }

    public int WantsPercent { get; }

    public int InvestPercent { get; }

    public double NeedsRatio => NeedsPercent / 100d;

    public double WantsRatio => WantsPercent / 100d;

    public double InvestRatio => InvestPercent / 100d;

    public double WantsArcRotationDegrees => NeedsPercent * 3.6d;

    public double InvestArcRotationDegrees => (NeedsPercent + WantsPercent) * 3.6d;

    internal PlanningSnapshot SessionSnapshot => _sessionSnapshot;

    public void AddIncome(IncomeLogVM income)
    {
        Incomes.Add(PlanningSnapshot.CopyIncome(income));
    }

    public void AddExpense(ExpenseVM expense)
    {
        Expenses.Add(PlanningSnapshot.CopyExpense(expense));
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
        if (e.PropertyName == nameof(ExpenseVM.Amount))
            RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        TotalIncome = Incomes.Sum(income => income.Amount);
        TotalExpenses = Expenses.Sum(expense => expense.Amount);
        Balance = TotalIncome - TotalExpenses;
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
