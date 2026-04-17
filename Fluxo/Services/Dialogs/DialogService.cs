using System.Windows;
using Fluxo.Resources.CustomControls;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxo.Services.Dialogs;

public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Func<Window?, string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> _showMessageBox;

    public DialogService(IServiceProvider serviceProvider)
        : this(serviceProvider, FluxoMessageBox.Show)
    {
    }

    public DialogService(IServiceProvider serviceProvider,
        Func<Window?, string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> showMessageBox)
    {
        _serviceProvider = serviceProvider;
        _showMessageBox = showMessageBox;
    }

    public TPopup GetPopupOfType<TPopup>() where TPopup : class
    {
        return _serviceProvider.GetRequiredService<TPopup>();
    }

    public bool? ShowPopupOfType<TPopup>(Window? owner = null) where TPopup : Window
    {
        var popup = GetPopupOfType<TPopup>();

        if (popup.Owner is null)
            popup.Owner = owner;

        return popup.ShowDialog();
    }

    public MessageBoxResult ShowWarning(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.OK)
    {
        return _showMessageBox(owner, message, title, buttons, MessageBoxImage.Warning);
    }

    public MessageBoxResult ShowError(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.OK)
    {
        return _showMessageBox(owner, message, title, buttons, MessageBoxImage.Error);
    }

    public MessageBoxResult ShowInformation(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.OK)
    {
        return _showMessageBox(owner, message, title, buttons, MessageBoxImage.Information);
    }

    public MessageBoxResult ShowQuestion(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.YesNo)
    {
        return _showMessageBox(owner, message, title, buttons, MessageBoxImage.Question);
    }
}
