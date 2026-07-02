using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Shell.Main;

public partial class FloatingNotificationItemVM(
    Guid id,
    string header,
    string message,
    IReadOnlyList<string> details,
    NotificationSeverity severity,
    Func<Task>? clickAsync,
    Func<FloatingNotificationItemVM, Task> closeAsync) : ObservableObject
{
    private int _activated;

    public Guid Id { get; } = id;
    public string Header { get; } = header;
    public string Message { get; } = message;
    public IReadOnlyList<string> Details { get; } = details;
    public NotificationSeverity Severity { get; } = severity;
    public bool HasDetails => Details.Count > 0;
    public bool IsActionable => clickAsync is not null;

    [ObservableProperty] private bool _isClosing;

    [RelayCommand]
    private async Task ActivateAsync()
    {
        if (Interlocked.Exchange(ref _activated, 1) != 0)
            return;

        if (clickAsync is not null)
            await clickAsync();

        await closeAsync(this);
    }
}
