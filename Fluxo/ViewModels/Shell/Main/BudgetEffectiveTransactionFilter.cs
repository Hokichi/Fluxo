using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Shell.Main;

internal static class BudgetEffectiveTransactionFilter
{
    internal static IEnumerable<TransactionVM> Select(IEnumerable<TransactionVM> transactions)
    {
        var included = transactions
            .Where(transaction => !transaction.IsForDeletion && !transaction.IsExcludedFromBudget)
            .ToList();
        var parentIds = included
            .Select(transaction => transaction.ParentTransactionId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        return included.Where(transaction => !parentIds.Contains(transaction.Id));
    }
}
