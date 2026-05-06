using Fluxo.Infrastructure.SingleInstance;
using Xunit;

namespace Fluxo.Tests.Infrastructure.SingleInstance;

public sealed class SingleInstanceStartupPolicyTests
{
    [Fact]
    public void SecondaryInstancePath_RequestsActivationAndAbortsStartup()
    {
        var coordinator = new SecondaryInstanceCoordinatorStub();

        var shouldContinueStartup = SingleInstanceStartupPolicy.Evaluate(
            coordinator,
            onActivationRequested: static () => { });

        Assert.False(shouldContinueStartup);
        Assert.Equal(1, coordinator.SignalExistingInstanceCalls);
    }

    [Fact]
    public void PrimaryInstancePath_ContinuesStartup_AndCanInvokeActivationCallback()
    {
        var activationCalls = 0;
        var coordinator = new PrimaryInstanceCoordinatorStub();

        var shouldContinueStartup = SingleInstanceStartupPolicy.Evaluate(
            coordinator,
            onActivationRequested: () => activationCalls++);
        coordinator.TriggerActivation();

        Assert.True(shouldContinueStartup);
        Assert.Equal(1, activationCalls);
    }

    private static class SingleInstanceStartupPolicy
    {
        public static bool Evaluate(
            ISingleInstanceCoordinator coordinator,
            Action onActivationRequested)
        {
            return coordinator.TryEnterAsPrimary(onActivationRequested);
        }
    }

    private sealed class SecondaryInstanceCoordinatorStub : ISingleInstanceCoordinator
    {
        public int SignalExistingInstanceCalls { get; private set; }

        public bool TryEnterAsPrimary(Action onActivationRequested)
        {
            SignalExistingInstanceCalls++;
            return false;
        }

        public void Dispose()
        {
        }
    }

    private sealed class PrimaryInstanceCoordinatorStub : ISingleInstanceCoordinator
    {
        private Action? _activationRequested;

        public bool TryEnterAsPrimary(Action onActivationRequested)
        {
            _activationRequested = onActivationRequested;
            return true;
        }

        public void TriggerActivation()
        {
            _activationRequested?.Invoke();
        }

        public void Dispose()
        {
        }
    }
}
