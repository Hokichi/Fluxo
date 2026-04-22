using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Shell.Main
{
    public partial class NotificationItemVM : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<NotificationVM> _notifications;
        [ObservableProperty] private string _message;
        [ObservableProperty] private int _count;
    }
}
