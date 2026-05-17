using Fluxo.Core.Interfaces.Services;
using Microsoft.Win32;

namespace Fluxo.Services.Ui;

public sealed class StartupRegistrationService : IStartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Fluxo";
    private const string StartupArgument = "--startup-tray";

    public void SetRunAtStartup(bool enabled)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                           ?? throw new InvalidOperationException("Unable to access the Windows startup registry key.");

        if (!enabled)
        {
            runKey.DeleteValue(RunValueName, throwOnMissingValue: false);
            return;
        }

        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
            throw new InvalidOperationException("Unable to resolve the current executable path.");

        runKey.SetValue(RunValueName, $"\"{processPath}\" {StartupArgument}", RegistryValueKind.String);
    }
}
