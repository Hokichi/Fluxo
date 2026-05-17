namespace Fluxo.Infrastructure.SingleInstance;

public interface ISingleInstanceCoordinator : IDisposable
{
    bool TryEnterAsPrimary(Action onActivationRequested);
}
