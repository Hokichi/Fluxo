namespace Fluxo.Services.Updates;

public interface IAppUpdateLifecycleService
{
    void LaunchUpdateInstallerAndShutdown(string installerPath);
}
