using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Views.Shell.Main.Sections;

public partial class FloatingNotificationList : UserControl
{
    public FloatingNotificationList()
    {
        InitializeComponent();
        DataContextChanged += (_, args) =>
        {
            if (args.OldValue is FloatingNotificationListVM oldVm)
                oldVm.Items.CollectionChanged -= OnItemsChanged;
            if (args.NewValue is FloatingNotificationListVM newVm)
                newVm.Items.CollectionChanged += OnItemsChanged;
        };
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(NotificationScrollViewer.ScrollToEnd);

    private void OnCardClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { DataContext: FloatingNotificationItemVM item } && item.ActivateCommand.CanExecute(null))
            item.ActivateCommand.Execute(null);
    }
}
