using WixToolset.BootstrapperApplicationApi;
using System.Threading;

namespace Fluxo.Installer;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Global\Fluxo.Installer.SingleInstance";

    private static int Main()
    {
        Mutex? singleInstanceMutex = null;
        try
        {
            singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
            if (!createdNew)
            {
                return 0;
            }

            void ReleaseSingleInstanceForElevationRelaunch()
            {
                // Burn invokes the bootstrapper on a native thread that may differ from this Main() thread.
                // ReleaseMutex() would throw (only the owning thread may release). Closing the handle abandons
                // the mutex so the elevated child can create it again; otherwise createdNew is false for the
                // child and we exit immediately while the parent handle is still open.
                singleInstanceMutex?.Dispose();
                singleInstanceMutex = null;
            }

            var bootstrapper = new InstallerBootstrapperApplication(ReleaseSingleInstanceForElevationRelaunch);
            ManagedBootstrapperApplication.Run(bootstrapper);
        }
        finally
        {
            singleInstanceMutex?.Dispose();
        }

        return 0;
    }
}
