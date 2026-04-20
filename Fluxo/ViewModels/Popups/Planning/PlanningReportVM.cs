using System.Collections.ObjectModel;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Popups.Planning;

public sealed class PlanningReportVM
{
    private readonly PlanningSnapshot _sessionSnapshot;

    public PlanningReportVM(PlanningSnapshot snapshot)
    {
        _sessionSnapshot = snapshot;
        Incomes = new ObservableCollection<IncomeLogVM>(snapshot.Incomes.Select(PlanningSnapshot.CopyIncome));
        Expenses = new ObservableCollection<ExpenseVM>(snapshot.Expenses.Select(PlanningSnapshot.CopyExpense));
        NeedsPercent = snapshot.NeedsPercent;
        WantsPercent = snapshot.WantsPercent;
        InvestPercent = snapshot.InvestPercent;
    }

    public ObservableCollection<IncomeLogVM> Incomes { get; }

    public ObservableCollection<ExpenseVM> Expenses { get; }

    public int NeedsPercent { get; }

    public int WantsPercent { get; }

    public int InvestPercent { get; }

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
        {
            return true;
        }

        var existing = Incomes.FirstOrDefault(x => x.Id == income.Id);
        return existing is not null && Incomes.Remove(existing);
    }

    public bool RemoveExpense(ExpenseVM expense)
    {
        if (Expenses.Remove(expense))
        {
            return true;
        }

        var existing = Expenses.FirstOrDefault(x => x.Id == expense.Id);
        return existing is not null && Expenses.Remove(existing);
    }
}
