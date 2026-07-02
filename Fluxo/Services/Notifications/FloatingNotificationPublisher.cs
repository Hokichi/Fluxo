using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Enums;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.Logging;

namespace Fluxo.Services.Notifications;

public static class FloatingNotificationPublisher
{
    public static void SaveFailed(IEnumerable<string> failures) => SaveFailed(WeakReferenceMessenger.Default, failures);

    public static void SaveFailed(IMessenger messenger, IEnumerable<string> failures)
    {
        Publish(messenger, "Save failed", string.Empty,
            failures.Where(value => !string.IsNullOrWhiteSpace(value))
                .SelectMany(value => value.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            NotificationSeverity.Warning);
    }

    public static void Success(IMessenger messenger, string header, string message, bool includeHistoryAction = false)
    {
        var body = includeHistoryAction
            ? $"{message.TrimEnd()} Click to view in History"
            : message;
        Publish(messenger, header, body, [], NotificationSeverity.Success,
            includeHistoryAction ? OpenHistoryAsync : null);
    }

    public static void Warning(IMessenger messenger, string header, string message) =>
        Publish(messenger, header, message, [], NotificationSeverity.Warning);

    public static void Warning(string header, string message) =>
        Warning(WeakReferenceMessenger.Default, header, message);

    public static void Success(string header, string message, bool includeHistoryAction = false) =>
        Success(WeakReferenceMessenger.Default, header, message, includeHistoryAction);

    public static void LoggedFailure(IMessenger messenger, Exception exception, string operation)
    {
        FluxoLogManager.LogError(exception, $"Unable to {operation}.");
        var path = Path.Combine(FluxoLogManager.GetIssuesLogsDirectoryPath(), FluxoLogManager.CurrentLogFileName);
        Publish(messenger, $"{operation} failed", "Click to open in Explorer", [],
            NotificationSeverity.Danger, () => OpenInExplorerAsync(path));
    }

    public static Guid Publish(
        IMessenger messenger,
        string header,
        string message,
        IReadOnlyList<string> details,
        NotificationSeverity severity,
        Func<Task>? clickAsync = null)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        if (string.IsNullOrWhiteSpace(header))
            throw new ArgumentException("Header cannot be empty.", nameof(header));

        var id = Guid.NewGuid();
        messenger.Send(new ShowFloatingNotificationMessage(new FloatingNotificationRequest(
            header.Trim(), message?.Trim() ?? string.Empty, details, severity, clickAsync, id)));
        return id;
    }

    public static void Dismiss(IMessenger messenger, Guid id)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        if (id != Guid.Empty)
            messenger.Send(new DismissFloatingNotificationMessage(id));
    }

    private static Task OpenHistoryAsync()
    {
        // TODO: Implement History panel hook here.
        // TODO: Exclude actions related to Settings from Undo/Redo.
        return Task.CompletedTask;
    }

    private static Task OpenInExplorerAsync(string path)
    {
        var target = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{Path.GetDirectoryName(path)}\"";
        Process.Start(new ProcessStartInfo("explorer.exe", target) { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
