using Fluxo.Core.Interfaces.Services;
using Serilog;
using Serilog.Events;
using System.Globalization;
using System.IO;

namespace Fluxo.Services.Logging;

public sealed class FluxoLogService : ILogService
{
    public string CurrentLogFileName => FluxoLogManager.CurrentLogFileName;

    public void Initialize(string? appUsername)
    {
        FluxoLogManager.Initialize(appUsername);
    }

    public void LogError(Exception exception, string message)
    {
        FluxoLogManager.LogError(exception, message);
    }

    public void LogWarning(Exception exception, string message)
    {
        FluxoLogManager.LogWarning(exception, message);
    }

    public void LogInformation(string message)
    {
        FluxoLogManager.LogInformation(message);
    }

    public string CreateFailureMessage(string performedProcess)
    {
        return FluxoLogManager.CreateFailureMessage(performedProcess);
    }

    public void LogFailureForProcess(Exception exception, string performedProcess)
    {
        FluxoLogManager.LogFailureForProcess(exception, performedProcess);
    }

    public void CloseAndFlush()
    {
        FluxoLogManager.CloseAndFlush();
    }
}

public static class FluxoLogManager
{
    private const string DefaultUsername = "User";
    private const string LogsDirectoryName = "Logs";
    private const string DateFormat = "MMddyyyy";

    public static string CurrentLogFileName { get; private set; } = BuildFileName(DefaultUsername, DateTime.Now);

    public static void Initialize(string? appUsername)
    {
        var now = DateTime.Now;
        var username = SanitizeUsername(appUsername);
        CurrentLogFileName = BuildFileName(username, now);

        var logsDirectory = Path.Combine(AppContext.BaseDirectory, LogsDirectoryName);
        Directory.CreateDirectory(logsDirectory);
        var fullLogPath = Path.Combine(logsDirectory, CurrentLogFileName);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: fullLogPath,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                shared: true)
            .CreateLogger();

        Log.Information("Fluxo logging initialized to {LogFileName}.", CurrentLogFileName);
    }

    public static void LogError(Exception exception, string message)
    {
        Log.Error(exception, "{Message}", message);
    }

    public static void LogWarning(Exception exception, string message)
    {
        Log.Warning(exception, "{Message}", message);
    }

    public static void LogInformation(string message)
    {
        Log.Information("{Message}", message);
    }

    public static string CreateFailureMessage(string performedProcess)
    {
        return $"Failed to {performedProcess}. Please refer to {CurrentLogFileName} for the issue's detailed.";
    }

    public static void LogFailureForProcess(Exception exception, string performedProcess)
    {
        LogError(exception, CreateFailureMessage(performedProcess));
    }

    public static void CloseAndFlush()
    {
        try
        {
            Log.CloseAndFlush();
        }
        catch
        {
            // Logging shutdown must never throw during app termination.
        }
    }

    private static string BuildFileName(string username, DateTime date)
    {
        return $"{username}{date.ToString(DateFormat, CultureInfo.InvariantCulture)}.log";
    }

    private static string SanitizeUsername(string? username)
    {
        var raw = string.IsNullOrWhiteSpace(username) ? DefaultUsername : username.Trim();
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitizedCharacters = raw.Where(character => !invalidCharacters.Contains(character)).ToArray();
        var sanitized = new string(sanitizedCharacters).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? DefaultUsername : sanitized;
    }
}
