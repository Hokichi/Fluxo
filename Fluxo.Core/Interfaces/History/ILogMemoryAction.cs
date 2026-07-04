using Fluxo.Core.Interfaces;

namespace Fluxo.Core.Interfaces.History;

public interface ILogMemoryAction
{
    string Description { get; }

    string Title => Description;

    string Summary => string.Empty;

    string Details => string.Empty;

    Task RevertAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default);

    Task ReapplyAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default);
}
