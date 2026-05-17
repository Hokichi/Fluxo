namespace Fluxo.Core.Interfaces.Services;

public interface ILogService
{
    string CurrentLogFileName { get; }

    void Initialize(string? appUsername);

    void LogError(Exception exception, string message);

    void LogWarning(Exception exception, string message);

    void LogInformation(string message);

    string CreateFailureMessage(string performedProcess);

    void LogFailureForProcess(Exception exception, string performedProcess);

    void CloseAndFlush();
}
