using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

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
        var dpi = VisualTreeHelper.GetDpi(this);
        var dpiScaleX = dpi.DpiScaleX <= 0 ? 1 : dpi.DpiScaleX;
        var dpiScaleY = dpi.DpiScaleY <= 0 ? 1 : dpi.DpiScaleY;

        var workArea = ResolveWorkAreaInDip(screenPoint, dpiScaleX, dpiScaleY);
        var anchorPointDip = ConvertDevicePointToDip(screenPoint, dpiScaleX, dpiScaleY);
        var popupOrigin = CalculatePopupOrigin(anchorPointDip, new Size(width, height), workArea, horizontalPadding, verticalPadding);

        Left = popupOrigin.X;
        Top = popupOrigin.Y;

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
        _autoCloseTimer.Tick -= OnAutoCloseTimerTick;
        Deactivated -= OnDeactivated;
        MouseEnter -= OnMouseEnter;
        MouseLeave -= OnMouseLeave;
        Closed -= OnClosed;
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

    private static double SafeClamp(double value, double min, double max)
    {
        if (max < min)
            return min;

        return Math.Clamp(value, min, max);
    }

    internal static Point CalculatePopupOrigin(
        Point anchorPointDip,
        Size popupSizeDip,
        Rect workAreaDip,
        double horizontalPadding = 12,
        double verticalPadding = 12)
    {
        var left = SafeClamp(
            anchorPointDip.X - popupSizeDip.Width + horizontalPadding,
            workAreaDip.Left,
            workAreaDip.Right - popupSizeDip.Width);

        var top = SafeClamp(
            anchorPointDip.Y - popupSizeDip.Height - verticalPadding,
            workAreaDip.Top,
            workAreaDip.Bottom - popupSizeDip.Height);

        return new Point(left, top);
    }

    internal static Rect ResolveWorkAreaInDip(
        Point screenPointPx,
        double dpiScaleX,
        double dpiScaleY,
        Func<Drawing.Point, Drawing.Rectangle>? monitorWorkAreaResolver = null)
    {
        monitorWorkAreaResolver ??= static point => Forms.Screen.FromPoint(point).WorkingArea;
        var screenPoint = new Drawing.Point((int)Math.Round(screenPointPx.X), (int)Math.Round(screenPointPx.Y));
        var monitorWorkArea = monitorWorkAreaResolver(screenPoint);

        return ConvertDeviceRectToDip(monitorWorkArea, dpiScaleX, dpiScaleY);
    }

    internal static Point ConvertDevicePointToDip(Point devicePoint, double dpiScaleX, double dpiScaleY)
    {
        var safeScaleX = dpiScaleX <= 0 ? 1 : dpiScaleX;
        var safeScaleY = dpiScaleY <= 0 ? 1 : dpiScaleY;

        return new Point(devicePoint.X / safeScaleX, devicePoint.Y / safeScaleY);
    }

    internal static Rect ConvertDeviceRectToDip(Drawing.Rectangle deviceRect, double dpiScaleX, double dpiScaleY)
    {
        var safeScaleX = dpiScaleX <= 0 ? 1 : dpiScaleX;
        var safeScaleY = dpiScaleY <= 0 ? 1 : dpiScaleY;

        return new Rect(
            deviceRect.Left / safeScaleX,
            deviceRect.Top / safeScaleY,
            deviceRect.Width / safeScaleX,
            deviceRect.Height / safeScaleY);
    }
}
