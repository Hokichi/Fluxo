namespace Fluxo.Tests.TestSupport;

internal static class WindowsPathFixtures
{
    public static string ProgramFilesFluxoFolder => Path.Combine(@"C:\Program Files", "fluxo");
    public static string InstalledExecutable => Path.Combine(ProgramFilesFluxoFolder, "fluxo.exe");
    public static string RepairerExecutable => Path.Combine(ProgramFilesFluxoFolder, "fluxo.Repairer.exe");
    public static string AppsFluxoFolder => Path.Combine(@"X:\Apps", "fluxo");
    public static string AppsFluxoFolderWithCapitalName => Path.Combine(@"X:\Apps", "Fluxo");
    public static string DownloadsInstaller => Path.Combine(@"X:\Downloads", "fluxo-1.0.0-Installer.exe");
    public static string DefaultInstaller => TempFile("fluxo-installer.exe");
    public static string CleanupScript => TempFile("fluxo-cleanup-test.cmd");
    public static string LocalAppDataFluxoFolder => Path.Combine(@"X:\Users\TestUser\AppData\Local", "fluxo");
    public static string ExtractedBundleProcess => Path.Combine(@"X:\Users\TestUser\AppData\Local\Temp", "{bundle}", "Fluxo.Installer.exe");
    public static string ExtractedBootstrapperProcess => Path.Combine(@"X:\Users\TestUser\AppData\Local\Temp", "{bundle}", ".ba", "Fluxo.Installer.exe");
    public static string BuildOutputInstaller => Path.Combine(
        @"X:\Source",
        "Fluxo",
        "Fluxo.Installer.Bundle",
        "bin",
        "x64",
        "Release",
        "fluxo-1.0.0-Installer.exe");
    public static string AlternateRepairerExecutable => Path.Combine(@"X:\Alternate", "fluxo.Repairer.exe");
    public static string UppercaseRepairerExecutable => Path.Combine(@"X:\Alternate", "Fluxo.REPAIRER.exe");

    public static string TempFile(string fileName) => Path.Combine(@"X:\Temp", fileName);

    public static string RuntimeListEntry(int majorVersion) =>
        $"Microsoft.NETCore.App {majorVersion}.0.0 [{Path.Combine(@"X:\dotnet", "shared", "Microsoft.NETCore.App")}]";
}
