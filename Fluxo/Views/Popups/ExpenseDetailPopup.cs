using Fluxo.Resources.CustomControls;
using Fluxo.ViewModels.Entities;

namespace Fluxo.Views.Popups;

public class ExpenseDetailPopup : BasePopup
{
    public ExpenseDetailPopup(ExpenseLogVM expenseLog)
    {
        PopupTitle = expenseLog.Expense?.Name ?? "Expense Detail";
    }
}
