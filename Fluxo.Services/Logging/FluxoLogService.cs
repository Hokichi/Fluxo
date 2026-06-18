using Fluxo.Core.Interfaces.Services;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Display;
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
    private const string AppDirectoryName = "fluxo";
    private const string LogsDirectoryName = "logs";
    private const string DbLogsDirectoryName = "db";
    private const string IssuesLogsDirectoryName = "issues";
    private const string OthersLogsDirectoryName = "others";
    private const string DateFormat = "MMddyyyy";
    private const string LogCategoryPropertyName = "FluxoLogCategory";
    private const string DatabaseLogCategory = "db";
    private const string IssuesLogCategory = "issues";
    private const string OthersLogCategory = "others";
    private static readonly MessageTemplateTextFormatter ExceptionFormatter = new(
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        CultureInfo.InvariantCulture);

    public static string CurrentLogFileName { get; private set; } = BuildFileName(DefaultUsername, DateTime.Now);

    public static void Initialize(string? appUsername)
    {
        var now = DateTime.Now;
        var username = SanitizeUsername(appUsername);
        CurrentLogFileName = BuildFileName(username, now);

        var logsDirectory = GetLogsDirectoryPath();
        var dbLogsDirectory = GetDbLogsDirectoryPath();
        var issuesLogsDirectory = GetIssuesLogsDirectoryPath();
        var othersLogsDirectory = GetOthersLogsDirectoryPath();
        Directory.CreateDirectory(logsDirectory);
        Directory.CreateDirectory(dbLogsDirectory);
        Directory.CreateDirectory(issuesLogsDirectory);
        Directory.CreateDirectory(othersLogsDirectory);

        var dbLogPath = Path.Combine(dbLogsDirectory, CurrentLogFileName);
        var issuesLogPath = Path.Combine(issuesLogsDirectory, CurrentLogFileName);
        var othersLogPath = Path.Combine(othersLogsDirectory, CurrentLogFileName);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Logger(logger => logger
                .Filter.ByIncludingOnly(logEvent => HasLogCategory(logEvent, DatabaseLogCategory))
                .WriteTo.File(
                    path: dbLogPath,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    shared: true))
            .WriteTo.Logger(logger => logger
                .Filter.ByIncludingOnly(logEvent => HasLogCategory(logEvent, IssuesLogCategory))
                .WriteTo.File(
                    path: issuesLogPath,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    shared: true))
            .WriteTo.Logger(logger => logger
                .Filter.ByIncludingOnly(logEvent => HasLogCategory(logEvent, OthersLogCategory))
                .WriteTo.File(
                    path: othersLogPath,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    shared: true))
            .CreateLogger();

        ForCategory(OthersLogCategory).Information("Fluxo logging initialized to {LogFileName}.", CurrentLogFileName);
    }

    public static string GetLogsDirectoryPath()
    {
        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (string.IsNullOrWhiteSpace(localAppData))
            localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(localAppData, AppDirectoryName, LogsDirectoryName);
    }

    public static string GetDbLogsDirectoryPath() => Path.Combine(GetLogsDirectoryPath(), DbLogsDirectoryName);

    public static string GetIssuesLogsDirectoryPath() => Path.Combine(GetLogsDirectoryPath(), IssuesLogsDirectoryName);

    public static string GetOthersLogsDirectoryPath() => Path.Combine(GetLogsDirectoryPath(), OthersLogsDirectoryName);

    public static void LogError(Exception exception, string message)
    {
        CurrentLogFileName = WriteExceptionLogFile(exception, message);
        ForCategory(IssuesLogCategory).Error(exception, "{Message}", message);
    }

    public static void LogWarning(Exception exception, string message)
    {
        CurrentLogFileName = WriteExceptionLogFile(exception, message);
        ForCategory(IssuesLogCategory).Warning(exception, "{Message}", message);
    }

    public static void LogInformation(string message)
    {
        var category = message.StartsWith("EF Core:", StringComparison.OrdinalIgnoreCase)
            ? DatabaseLogCategory
            : OthersLogCategory;

        ForCategory(category).Information("{Message}", message);
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

    private static ILogger ForCategory(string category)
    {
        return Log.ForContext(LogCategoryPropertyName, category);
    }

    private static bool HasLogCategory(LogEvent logEvent, string category)
    {
        return logEvent.Properties.TryGetValue(LogCategoryPropertyName, out var value) &&
               string.Equals(value.ToString().Trim('"'), category, StringComparison.Ordinal);
    }

    private static string WriteExceptionLogFile(Exception exception, string message)
    {
        var now = DateTime.Now;
        var exceptionFileName = $"fluxo_exception_{now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.log";
        var exceptionDirectory = GetIssuesLogsDirectoryPath();
        Directory.CreateDirectory(exceptionDirectory);
        var exceptionPath = Path.Combine(exceptionDirectory, exceptionFileName);

        var logEvent = new LogEvent(
            DateTimeOffset.Now,
            LogEventLevel.Error,
            exception,
            new Serilog.Parsing.MessageTemplateParser().Parse("{Message}"),
            [new LogEventProperty("Message", new Serilog.Events.ScalarValue(message))]);

        using var writer = new StreamWriter(exceptionPath, append: true);
        ExceptionFormatter.Format(logEvent, writer);
        writer.WriteLine();

        return exceptionFileName;
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
