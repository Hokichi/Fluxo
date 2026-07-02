using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Resources.Resources.Messages;

namespace Fluxo.ViewModels.Shell.Main;

public partial class FloatingNotificationListVM : ObservableObject,
    IRecipient<ShowFloatingNotificationMessage>, IRecipient<DismissFloatingNotificationMessage>, IDisposable
{
    private readonly IMessenger _messenger;
    private readonly TimeSpan _lifetime;
    private readonly TimeSpan _fadeDuration;
    private readonly CancellationTokenSource _disposeToken = new();

    public FloatingNotificationListVM(IMessenger messenger)
        : this(messenger, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200))
    {
    }

    public FloatingNotificationListVM(IMessenger messenger, TimeSpan lifetime, TimeSpan fadeDuration)
    {
        _messenger = messenger;
        _lifetime = lifetime;
        _fadeDuration = fadeDuration;
        messenger.RegisterAll(this);
    }

    public ObservableCollection<FloatingNotificationItemVM> Items { get; } = [];

    [ObservableProperty] private FloatingNotificationItemVM? _scrollTarget;
    [ObservableProperty] private bool _hasItems;

    public void Receive(ShowFloatingNotificationMessage message)
    {
        var value = message.Value;
        var item = new FloatingNotificationItemVM(
            value.Id, value.Header, value.Message, value.Details, value.Severity, value.ClickAsync, CloseAsync);
        Items.Add(item);
        HasItems = true;
        ScrollTarget = item;
        _ = ExpireAsync(item, _disposeToken.Token);
    }

    public void Receive(DismissFloatingNotificationMessage message)
    {
        var item = Items.FirstOrDefault(candidate => candidate.Id == message.Value);
        if (item is not null)
            _ = CloseAsync(item);
    }

    public void Dispose()
    {
        _disposeToken.Cancel();
        _disposeToken.Dispose();
        _messenger.UnregisterAll(this);
    }

    private async Task ExpireAsync(FloatingNotificationItemVM item, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_lifetime, cancellationToken);
            await CloseAsync(item);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CloseAsync(FloatingNotificationItemVM item)
    {
        if (item.IsClosing || !Items.Contains(item))
            return;

        item.IsClosing = true;
        if (_fadeDuration > TimeSpan.Zero)
            await Task.Delay(_fadeDuration);

        Items.Remove(item);
        HasItems = Items.Count > 0;
        ScrollTarget = Items.LastOrDefault();
    }
}
