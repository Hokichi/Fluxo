using Fluxo.Core.Interfaces.Services;
using Fluxo.Extensions;
using Fluxo.Services.Persistence;
using Fluxo.Services.Notifications;
using Fluxo.ViewModels.Shell.Main;
using Fluxo.Views.Shell.Main;
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

    [Fact]
    public void AddFluxoPresentation_RegistersCalendarService()
    {
        var services = new ServiceCollection();

        services.AddFluxoPresentation();

        var descriptor = services.LastOrDefault(sd => sd.ServiceType == typeof(ICalendarService));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(CalendarService), descriptor!.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddUIData_RegistersCalendarViewAndViewModel()
    {
        var services = new ServiceCollection();

        services.AddUIData();

        var calendarVmDescriptor = services.LastOrDefault(sd => sd.ServiceType == typeof(CalendarVM));
        var calendarViewDescriptor = services.LastOrDefault(sd => sd.ServiceType == typeof(Calendar));

        Assert.NotNull(calendarVmDescriptor);
        Assert.Equal(ServiceLifetime.Transient, calendarVmDescriptor!.Lifetime);
        Assert.NotNull(calendarViewDescriptor);
        Assert.Equal(ServiceLifetime.Transient, calendarViewDescriptor!.Lifetime);
    }
}
