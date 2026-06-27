using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class TransactionService(IDataOperationRunner runner, IMapper mapper) : ITransactionService
{
    public Task<IReadOnlyList<TransactionDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        runner.RunAsync("load transactions", async (scope, ct) =>
            mapper.Map<IReadOnlyList<TransactionDto>>(await scope.UnitOfWork.Transactions.GetAllAsync(ct)), cancellationToken);

    public Task<TransactionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        runner.RunAsync("load transaction", async (scope, ct) =>
        {
            var transaction = await scope.UnitOfWork.Transactions.GetByIdAsync(id, ct);
            return transaction is null ? null : mapper.Map<TransactionDto>(transaction);
        }, cancellationToken);

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        runner.RunAsync("delete transaction", async (scope, ct) =>
        {
            var transaction = await scope.UnitOfWork.Transactions.GetByIdAsync(id, ct);
            if (transaction is null)
                return;

            transaction.IsForDeletion = true;
            scope.UnitOfWork.Transactions.Update(transaction);
            await scope.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

    public Task PostTerminationCleanupAsync(CancellationToken cancellationToken = default) =>
        runner.RunAsync("cleanup terminated transactions", async (scope, ct) =>
        {
            var transactions = await scope.UnitOfWork.Transactions.GetMarkedForDeletionAsync(ct);
            foreach (var transaction in transactions)
                scope.UnitOfWork.Transactions.Remove(transaction);
            if (transactions.Count > 0)
                await scope.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
}
