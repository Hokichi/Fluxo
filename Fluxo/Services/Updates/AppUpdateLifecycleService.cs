using System.Windows;

namespace Fluxo.Services.Updates;

public sealed class AppUpdateLifecycleService : IAppUpdateLifecycleService
{
    public void LaunchUpdateInstallerAndShutdown(string installerPath)
    {
        if (Application.Current is not App app)
            throw new InvalidOperationException("The Fluxo application instance is unavailable.");

        app.LaunchUpdateInstallerAndShutdown(installerPath);
    }
}
