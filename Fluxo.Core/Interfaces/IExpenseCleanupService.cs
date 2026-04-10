namespace Fluxo.Core.Interfaces;

public interface IExpenseCleanupService
{
    Task DeleteMarkedExpenseLogsAsync(IEnumerable<int> expenseLogIdsMarkedForDeletion,
        CancellationToken cancellationToken = default);
}
