using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Popups.Planning;

public sealed class PlanningSnapshot
{
    public PlanningSnapshot(
        IEnumerable<IncomeLogVM> incomes,
        IEnumerable<ExpenseVM> expenses,
        int needsPercent,
        int wantsPercent,
        int investPercent,
        bool fixedExpensesLoaded = false,
        IReadOnlyDictionary<int, ExpenseVM>? cachedFixedExpenses = null,
        IEnumerable<int>? importedFixedExpenseIds = null)
    {
        Incomes = incomes.Select(CopyIncome).ToList();
        Expenses = expenses.Select(CopyExpense).ToList();
        NeedsPercent = needsPercent;
        WantsPercent = wantsPercent;
        InvestPercent = investPercent;
        FixedExpensesLoaded = fixedExpensesLoaded;
        CachedFixedExpenses = (cachedFixedExpenses ?? new Dictionary<int, ExpenseVM>())
            .ToDictionary(pair => pair.Key, pair => CopyExpense(pair.Value));
        ImportedFixedExpenseIds = (importedFixedExpenseIds ?? [])
            .ToHashSet();
    }

    public IReadOnlyList<IncomeLogVM> Incomes { get; }
    public IReadOnlyList<ExpenseVM> Expenses { get; }
    public int NeedsPercent { get; }
    public int WantsPercent { get; }
    public int InvestPercent { get; }
    public bool FixedExpensesLoaded { get; }
    public IReadOnlyDictionary<int, ExpenseVM> CachedFixedExpenses { get; }
    public IReadOnlySet<int> ImportedFixedExpenseIds { get; }

    public PlanningSnapshot DeepCopy()
    {
        return new PlanningSnapshot(
            Incomes,
            Expenses,
            NeedsPercent,
            WantsPercent,
            InvestPercent,
            FixedExpensesLoaded,
            CachedFixedExpenses,
            ImportedFixedExpenseIds);
    }

    internal static IncomeLogVM CopyIncome(IncomeLogVM source)
    {
        return new IncomeLogVM
        {
            Id = source.Id,
            Amount = source.Amount,
            AddedOn = source.AddedOn,
            Notes = source.Notes,
            SpendingSource = CopySpendingSource(source.SpendingSource)
        };
    }

    internal static ExpenseVM CopyExpense(ExpenseVM source)
    {
        return new ExpenseVM
        {
            Id = source.Id,
            Name = source.Name,
            Amount = source.Amount,
            ExpenseKind = source.ExpenseKind,
            ExpenseCategory = source.ExpenseCategory,
            RecurringDate = source.RecurringDate,
            IsActive = source.IsActive,
            ExpenseTag = CopyExpenseTag(source.ExpenseTag),
            SpendingSource = CopySpendingSource(source.SpendingSource)
        };
    }

    private static ExpenseTagVM CopyExpenseTag(ExpenseTagVM source)
    {
        return new ExpenseTagVM
        {
            Id = source.Id,
            Name = source.Name,
            HexCode = source.HexCode,
            IconName = source.IconName,
            IsSystemTag = source.IsSystemTag
        };
    }

    private static SpendingSourceVM CopySpendingSource(SpendingSourceVM source)
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
            IsEnabled = source.IsEnabled,
            MoneyIn = source.MoneyIn,
            MoneyOut = source.MoneyOut,
            IsSelected = source.IsSelected
        };
    }
}
