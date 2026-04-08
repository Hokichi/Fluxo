using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Notifications;

public partial class NotificationItemVM : ObservableObject
{
    [ObservableProperty] private DateTime _createdOn = DateTime.Now;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private NotificationSeverity _severity;

    [ObservableProperty] private string _title = string.Empty;
    public string Key { get; init; } = string.Empty;

    public bool IsSystemGenerated { get; init; }
}