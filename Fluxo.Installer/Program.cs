using WixToolset.BootstrapperApplicationApi;
using System.Threading;

namespace Fluxo.Installer;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Global\Fluxo.Installer.SingleInstance";

    private static int Main()
    {
        using var singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            return 0;
        }

        var bootstrapper = new InstallerBootstrapperApplication(singleInstanceMutex.ReleaseMutex);
        ManagedBootstrapperApplication.Run(bootstrapper);
        return 0;
    }
}
