using System.Windows;

namespace Fluxo.Views.Popups;

public static class FluxoMessageBox
{
    public static MessageBoxResult Show(Window? owner, string message, string title,
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
    {
        var dialog = new MessageBoxPopup(message, title, buttons, icon)
        {
            Owner = owner ?? GetActiveWindow()
        };

        dialog.ShowDialog();
        return dialog.Result;
    }

    private static Window? GetActiveWindow()
    {
        return Application.Current?.Windows
                   .OfType<Window>()
                   .FirstOrDefault(window => window.IsActive)
               ?? Application.Current?.MainWindow;
    }
}