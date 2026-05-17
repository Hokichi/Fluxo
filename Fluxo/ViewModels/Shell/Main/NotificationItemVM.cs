using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Shell.Main
{
    public partial class NotificationItemVM : ObservableObject
    {
        [ObservableProperty] private NotificationGroupCategory _category;
        [ObservableProperty] private ObservableCollection<NotificationVM> _notifications = [];
        [ObservableProperty] private string _header = string.Empty;
        [ObservableProperty] private string _message = string.Empty;
        [ObservableProperty] private int _count;
        [ObservableProperty] private NotificationSeverity _severity;
        [ObservableProperty] private bool _hasActionCta;
        [ObservableProperty] private DateTime _latestCreatedOn;
        [ObservableProperty] private bool _isBusy;
    }
}
