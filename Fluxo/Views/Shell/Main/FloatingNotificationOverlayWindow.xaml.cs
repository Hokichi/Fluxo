using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Views.Shell.Main;

public partial class FloatingNotificationOverlayWindow : Window
{
    private const double Inset = 24;
    private readonly FloatingNotificationListVM _viewModel;
    private readonly DispatcherTimer _activeTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private MainWindow? _mainWindow;

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    public FloatingNotificationOverlayWindow(FloatingNotificationListVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        NotificationList.DataContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _activeTimer.Tick += (_, _) => RefreshVisibility();
    }

    public void Attach(MainWindow mainWindow)
    {
        if (ReferenceEquals(_mainWindow, mainWindow))
            return;
        _mainWindow = mainWindow;
        mainWindow.LocationChanged += OnOwnerBoundsChanged;
        mainWindow.SizeChanged += OnOwnerBoundsChanged;
        mainWindow.StateChanged += OnOwnerStateChanged;
        SizeChanged += OnOwnerBoundsChanged;
        _activeTimer.Start();
        RefreshVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FloatingNotificationListVM.HasItems))
            RefreshVisibility();
    }

    private void OnOwnerBoundsChanged(object? sender, EventArgs e) => UpdatePosition();
    private void OnOwnerBoundsChanged(object sender, SizeChangedEventArgs e) => UpdatePosition();
    private void OnOwnerStateChanged(object? sender, EventArgs e) => RefreshVisibility();

    private void RefreshVisibility()
    {
        if (_mainWindow is null)
            return;
        var show = _viewModel.HasItems && _mainWindow.IsVisible &&
                   _mainWindow.WindowState != WindowState.Minimized && IsApplicationForeground();
        if (show)
        {
            if (!IsVisible) Show();
            UpdatePosition();
        }
        else if (IsVisible)
        {
            Hide();
        }
    }

    internal static bool IsForegroundProcess(uint foregroundProcessId, int currentProcessId) =>
        foregroundProcessId != 0 && foregroundProcessId == (uint)currentProcessId;

    private static bool IsApplicationForeground()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out var processId);
        return IsForegroundProcess(processId, Environment.ProcessId);
    }

    private void UpdatePosition()
    {
        if (_mainWindow is null || !_mainWindow.IsVisible)
            return;
        var pixelPoint = _mainWindow.PointToScreen(new Point(_mainWindow.ActualWidth, _mainWindow.ActualHeight));
        var transform = PresentationSource.FromVisual(_mainWindow)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var point = transform.Transform(pixelPoint);
        Left = point.X - ActualWidth - Inset;
        Top = point.Y - ActualHeight - Inset;
    }
}
