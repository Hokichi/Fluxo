using WixToolset.BootstrapperApplicationApi;

namespace Fluxo.Installer;

internal static class Program
{
    private static int Main()
    {
        var bootstrapper = new InstallerBootstrapperApplication();
        ManagedBootstrapperApplication.Run(bootstrapper);
        return 0;
    }
}
