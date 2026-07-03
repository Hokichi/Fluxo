using Fluxo.Core.Interfaces;

namespace Fluxo.Core.Interfaces.History;

public interface ILogMemoryAction
{
    string Description { get; }

    Task RevertAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default);

    Task ReapplyAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default);
}
