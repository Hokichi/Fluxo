using Fluxo.Core.Interfaces;

namespace Fluxo.Core.Interfaces.History;

public interface ILogMemoryAction
{
    string Description { get; }

    Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default);

    Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default);
}
