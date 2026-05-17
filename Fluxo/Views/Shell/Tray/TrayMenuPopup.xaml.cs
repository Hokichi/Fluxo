using System.Windows;

namespace Fluxo.Views.Shell.Tray;

public partial class TrayMenuPopup : Window
{
    public TrayMenuPopup()
    {
        InitializeComponent();
        Deactivated += (_, _) => Hide();
    }

    public event EventHandler? OpenFluxoRequested;
    public event EventHandler? RestartFluxoRequested;
    public event EventHandler? ExitRequested;

    public void ShowNearScreenPoint(Point screenPoint)
    {
        const double horizontalPadding = 12;
        const double verticalPadding = 12;

        // Avoid explicit Window.Measure() because tray callbacks can arrive from a
        // non-WPF context and manual top-level layout here has proven unstable.
        var width = Math.Max(ActualWidth, 240);
        var height = Math.Max(ActualHeight, 220);
        var workArea = SystemParameters.WorkArea;

        Left = Math.Clamp(screenPoint.X - width + horizontalPadding, workArea.Left, workArea.Right - width);
        Top = Math.Clamp(screenPoint.Y - height - verticalPadding, workArea.Top, workArea.Bottom - height);

        if (!IsVisible)
            Show();
        else
            Activate();
    }

    private void OnOpenFluxoClick(object sender, RoutedEventArgs e)
    {
        Hide();
        OpenFluxoRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRestartFluxoClick(object sender, RoutedEventArgs e)
    {
        Hide();
        RestartFluxoRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Hide();
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }
}
