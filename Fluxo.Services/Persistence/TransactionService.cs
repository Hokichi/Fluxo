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
}
