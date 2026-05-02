using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Entities;

public partial class NotificationVM : ObservableObject
{
    [ObservableProperty] private DateTime _createdOn = DateTime.Now;
    [ObservableProperty] private string _header = string.Empty;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private string _type = string.Empty;
    [ObservableProperty] private NotificationSeverity _severity;
    [ObservableProperty] private bool _isCleared;
}