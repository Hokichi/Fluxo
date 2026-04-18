using Fluxo.ViewModels.Messages;

namespace Fluxo.ViewModels.Shell;

public partial class MainVM
{
    private void HandleExpenseDetailUpdatedMessage(ExpenseDetailUpdatedMessage message)
    {
        if (!message.Value.HasChanges)
            return;

        _ = ReloadCurrentDataAsync();
    }
}
