using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class AccountService(IDataOperationRunner dataOperationRunner, IMapper mapper) : IAccountService
{
    public async Task<IReadOnlyList<AccountDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync("load accounts", async (scope, ct) =>
        {
            var sources = await scope.UnitOfWork.Accounts.GetAllAsync(ct);
            return mapper.Map<IReadOnlyList<AccountDto>>(sources);
        }, cancellationToken);
    }

    public async Task<AccountDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync("load account", async (scope, ct) =>
        {
            var source = await scope.UnitOfWork.Accounts.GetByIdAsync(id, ct);
            return source is null ? null : mapper.Map<AccountDto>(source);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountDto>> SearchAsync(AccountFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync("search accounts", async (scope, ct) =>
        {
            var sources = await scope.UnitOfWork.Accounts.SearchAsync(filter, ct);
            return mapper.Map<IReadOnlyList<AccountDto>>(sources);
        }, cancellationToken);
    }

    public async Task AddAsync(AccountDto dto, CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync("add account", async (scope, ct) =>
        {
            var source = mapper.Map<Account>(dto);
            source.Id = 0; // ensure EF treats this as a new insert
            await scope.UnitOfWork.Accounts.AddAsync(source, ct);
            await scope.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }
}
