using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Popups.Planning;

public partial class PlanningPopupVM : ObservableObject, IDisposable
{
    public const int DefaultNeedsPercent = 50;
    public const int DefaultWantsPercent = 30;
    public const int DefaultInvestPercent = 20;

    private readonly Dictionary<int, ExpenseVM> _cachedFixedExpenses = [];
    private readonly HashSet<int> _importedFixedExpenseIds = [];
    private readonly List<ExpenseVM> _trackedExpenseSubscriptions = [];
    private readonly List<IncomeLogVM> _trackedIncomeSubscriptions = [];
    private readonly IUnitOfWork _unitOfWork;
    private bool _fixedExpensesLoaded;
    private bool _disposed;

    [ObservableProperty] private int _investPercent = DefaultInvestPercent;
    [ObservableProperty] private int _needsPercent = DefaultNeedsPercent;
    [ObservableProperty] private int _wantsPercent = DefaultWantsPercent;

    public PlanningPopupVM(IUnitOfWork unitOfWork, PlanningSnapshot? snapshot = null)
    {
        _unitOfWork = unitOfWork;
        Incomes.CollectionChanged += OnIncomesChanged;
        Expenses.CollectionChanged += OnExpensesChanged;

        if (snapshot is not null)
            ApplySnapshot(snapshot);
    }

    public ObservableCollection<IncomeLogVM> Incomes { get; } = [];
    public ObservableCollection<ExpenseVM> Expenses { get; } = [];
    public bool IsAllocationValid => NeedsPercent + WantsPercent + InvestPercent == 100;
    public decimal TotalIncome => Incomes.Sum(income => income.Amount);
    public bool HasAnyInput =>
        Incomes.Any(income => income.Amount != 0m) ||
        Expenses.Any(expense => expense.Amount != 0m || !string.IsNullOrWhiteSpace(expense.Name)) ||
        NeedsPercent != DefaultNeedsPercent ||
        WantsPercent != DefaultWantsPercent ||
        InvestPercent != DefaultInvestPercent;

    public async Task SetImportFixedExpensesAsync(bool isChecked)
    {
        if (!isChecked)
        {
            RemoveImportedFixedExpenses();
            return;
        }

        if (!_fixedExpensesLoaded)
        {
            var expenses = await _unitOfWork.Expenses.GetAllAsync();
            foreach (var expense in expenses.Where(expense => expense.ExpenseKind == ExpenseKind.Fixed))
                _cachedFixedExpenses[expense.Id] = MapExpense(expense);

            _fixedExpensesLoaded = true;
        }

        foreach (var (id, expense) in _cachedFixedExpenses)
        {
            if (Expenses.Any(current => current.Id == id))
                continue;

            Expenses.Add(PlanningSnapshot.CopyExpense(expense));
            _importedFixedExpenseIds.Add(id);
        }
    }

    public bool ShouldPromptCloseOnMissingIncome()
    {
        return Incomes.Count == 0 || TotalIncome <= 0m;
    }

    public PlanningSnapshot BuildSnapshot()
    {
        return new PlanningSnapshot(
            Incomes,
            Expenses,
            NeedsPercent,
            WantsPercent,
            InvestPercent,
            _fixedExpensesLoaded,
            _cachedFixedExpenses,
            _importedFixedExpenseIds);
    }

    partial void OnNeedsPercentChanged(int value)
    {
        OnPropertyChanged(nameof(IsAllocationValid));
        OnPropertyChanged(nameof(HasAnyInput));
    }

    partial void OnWantsPercentChanged(int value)
    {
        OnPropertyChanged(nameof(IsAllocationValid));
        OnPropertyChanged(nameof(HasAnyInput));
    }

    partial void OnInvestPercentChanged(int value)
    {
        OnPropertyChanged(nameof(IsAllocationValid));
        OnPropertyChanged(nameof(HasAnyInput));
    }

    private void ApplySnapshot(PlanningSnapshot snapshot)
    {
        NeedsPercent = snapshot.NeedsPercent;
        WantsPercent = snapshot.WantsPercent;
        InvestPercent = snapshot.InvestPercent;

        foreach (var income in snapshot.Incomes)
            Incomes.Add(PlanningSnapshot.CopyIncome(income));

        foreach (var expense in snapshot.Expenses)
            Expenses.Add(PlanningSnapshot.CopyExpense(expense));

        foreach (var (id, expense) in snapshot.CachedFixedExpenses)
            _cachedFixedExpenses[id] = PlanningSnapshot.CopyExpense(expense);

        foreach (var id in snapshot.ImportedFixedExpenseIds)
            _importedFixedExpenseIds.Add(id);

        _fixedExpensesLoaded = snapshot.FixedExpensesLoaded;
    }

    private static ExpenseVM MapExpense(Expense expense)
    {
        return new ExpenseVM
        {
            Id = expense.Id,
            Name = expense.Name,
            Amount = expense.Amount,
            ExpenseKind = expense.ExpenseKind,
            ExpenseCategory = expense.ExpenseCategory,
            RecurringDate = expense.RecurringDate,
            IsActive = expense.IsActive,
            ExpenseTag = expense.ExpenseTag is null ? new ExpenseTagVM() : MapExpenseTag(expense.ExpenseTag),
            SpendingSource = expense.SpendingSource is null ? new SpendingSourceVM() : MapSpendingSource(expense.SpendingSource)
        };
    }

    private static ExpenseTagVM MapExpenseTag(ExpenseTag tag)
    {
        return new ExpenseTagVM
        {
            Id = tag.Id,
            Name = tag.Name,
            HexCode = tag.HexCode,
            IconName = tag.IconName,
            IsSystemTag = tag.IsSystemTag
        };
    }

    private static SpendingSourceVM MapSpendingSource(SpendingSource source)
    {
        return new SpendingSourceVM
        {
            Id = source.Id,
            Name = source.Name,
            SpendingSourceType = source.SpendingSourceType,
            AccountLimit = source.AccountLimit,
            SpentAmount = source.SpentAmount,
            Balance = source.Balance,
            MonthlyDueDate = source.MonthlyDueDate,
            DeductSource = source.DeductSource,
            InterestRate = source.InterestRate,
            ShowOnUI = source.ShowOnUI,
            IsEnabled = source.IsEnabled
        };
    }

    private void OnIncomesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildIncomeSubscriptions();
        OnPropertyChanged(nameof(TotalIncome));
        OnPropertyChanged(nameof(HasAnyInput));
    }

    private void OnExpensesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildExpenseSubscriptions();
        OnPropertyChanged(nameof(HasAnyInput));
    }

    private void RebuildIncomeSubscriptions()
    {
        foreach (var income in _trackedIncomeSubscriptions)
            income.PropertyChanged -= OnIncomeChanged;

        _trackedIncomeSubscriptions.Clear();

        foreach (var income in Incomes)
        {
            income.PropertyChanged += OnIncomeChanged;
            _trackedIncomeSubscriptions.Add(income);
        }
    }

    private void RebuildExpenseSubscriptions()
    {
        foreach (var expense in _trackedExpenseSubscriptions)
            expense.PropertyChanged -= OnExpenseChanged;

        _trackedExpenseSubscriptions.Clear();

        foreach (var expense in Expenses)
        {
            expense.PropertyChanged += OnExpenseChanged;
            _trackedExpenseSubscriptions.Add(expense);
        }
    }

    private void OnIncomeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IncomeLogVM.Amount))
        {
            OnPropertyChanged(nameof(TotalIncome));
            OnPropertyChanged(nameof(HasAnyInput));
        }
    }

    private void OnExpenseChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ExpenseVM.Amount) or nameof(ExpenseVM.Name))
            OnPropertyChanged(nameof(HasAnyInput));
    }

    private void RemoveImportedFixedExpenses()
    {
        for (var index = Expenses.Count - 1; index >= 0; index--)
        {
            var expense = Expenses[index];
            if (_importedFixedExpenseIds.Contains(expense.Id) && expense.ExpenseKind == ExpenseKind.Fixed)
                Expenses.RemoveAt(index);
        }

        _importedFixedExpenseIds.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Incomes.CollectionChanged -= OnIncomesChanged;
        Expenses.CollectionChanged -= OnExpensesChanged;

        foreach (var income in _trackedIncomeSubscriptions)
            income.PropertyChanged -= OnIncomeChanged;

        foreach (var expense in _trackedExpenseSubscriptions)
            expense.PropertyChanged -= OnExpenseChanged;

        _trackedIncomeSubscriptions.Clear();
        _trackedExpenseSubscriptions.Clear();
        _disposed = true;
    }
}
