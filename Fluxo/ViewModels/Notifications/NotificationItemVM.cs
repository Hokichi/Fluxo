using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Notifications;

public partial class NotificationItemVM : ObservableObject
{
    public string Key { get; init; } = string.Empty;

    public bool IsSystemGenerated { get; init; }

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private NotificationSeverity _severity;
    [ObservableProperty] private DateTime _createdOn = DateTime.Now;
}
