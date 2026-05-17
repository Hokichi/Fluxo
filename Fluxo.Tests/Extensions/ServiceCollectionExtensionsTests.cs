using Fluxo.Extensions;
using Fluxo.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fluxo.Tests.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddUIData_RegistersStartupNotificationSummaryService()
    {
        var services = new ServiceCollection();

        services.AddUIData();

        var descriptor = services.LastOrDefault(sd => sd.ServiceType == typeof(IStartupNotificationSummaryService));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(StartupNotificationSummaryService), descriptor!.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }
}
