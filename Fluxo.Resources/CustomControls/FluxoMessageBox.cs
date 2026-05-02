using System.Windows;

namespace Fluxo.Resources.CustomControls;

public static class FluxoMessageBox
{
    public static MessageBoxResult Show(Window? owner, string message, string title,
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        => MessageBox.Show(owner ?? GetActiveWindow(), message, title, buttons, icon);

    private static Window? GetActiveWindow()
    {
        return Application.Current?.Windows
                   .OfType<Window>()
                   .FirstOrDefault(window => window.IsActive)
               ?? Application.Current?.MainWindow;
    }
}
