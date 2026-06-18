using Fluxo.Core.DTO;
using Fluxo.Core.Filters;

namespace Fluxo.Core.Interfaces.Services;

public interface IAccountService
{
    Task<IReadOnlyList<AccountDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AccountDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountDto>> SearchAsync(AccountFilter filter, CancellationToken cancellationToken = default);
    Task AddAsync(AccountDto dto, CancellationToken cancellationToken = default);
}
