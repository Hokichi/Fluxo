using System.Windows;
using System.Windows.Threading;

namespace Fluxo.Views.Shell.Tray;

public partial class StartupNotificationPopup : Window
{
    private readonly DispatcherTimer _autoCloseTimer;

    public StartupNotificationPopup()
    {
        InitializeComponent();

        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _autoCloseTimer.Tick += OnAutoCloseTimerTick;

        Deactivated += OnDeactivated;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
        Closed += OnClosed;
    }

    public event EventHandler? OpenAppRequested;
    public event EventHandler? DismissRequested;

    public string SummaryText
    {
        get => SummaryTextBlock.Text;
        set => SummaryTextBlock.Text = value;
    }

    public void ShowNearScreenPoint(Point screenPoint)
    {
        const double horizontalPadding = 12;
        const double verticalPadding = 12;

        var width = Math.Max(ActualWidth, 320);
        var height = Math.Max(ActualHeight, 120);
        var workArea = SystemParameters.WorkArea;

        Left = Math.Clamp(screenPoint.X - width + horizontalPadding, workArea.Left, workArea.Right - width);
        Top = Math.Clamp(screenPoint.Y - height - verticalPadding, workArea.Top, workArea.Bottom - height);

        if (!IsVisible)
            Show();
        else
            Activate();

        RestartAutoCloseTimer();
    }

    private void OnOpenAppClick(object sender, RoutedEventArgs e)
    {
        HidePopup();
        OpenAppRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        HidePopup();
        DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnAutoCloseTimerTick(object? sender, EventArgs e)
    {
        HidePopup();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        HidePopup();
    }

    private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _autoCloseTimer.Stop();
    }

    private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        RestartAutoCloseTimer();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _autoCloseTimer.Stop();
    }

    private void HidePopup()
    {
        _autoCloseTimer.Stop();
        Hide();
    }

    private void RestartAutoCloseTimer()
    {
        _autoCloseTimer.Stop();
        _autoCloseTimer.Start();
    }
}
