namespace Fluxo.Infrastructure.SingleInstance;

public static class SingleInstanceStartupPolicy
{
    public static bool ShouldContinueStartup(
        ISingleInstanceCoordinator coordinator,
        Action onActivationRequested)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(onActivationRequested);

        return coordinator.TryEnterAsPrimary(onActivationRequested);
    }
}
