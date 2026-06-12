using System.Windows;
using System.Windows.Controls;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Views.Shell.Main.Pages;

public partial class Calendar : UserControl
{
    public static readonly DependencyProperty UseExpandedCalendarLayoutProperty = DependencyProperty.Register(
        nameof(UseExpandedCalendarLayout),
        typeof(bool),
        typeof(Calendar),
        new PropertyMetadata(false));

    private const double ExpandedLayoutMinWidth = 1320;
    private readonly CalendarVM _viewModel;
    private readonly SemaphoreSlim _openPreparationGate = new(1, 1);

    public Calendar(CalendarVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnCalendarLoaded;
        SizeChanged += OnCalendarSizeChanged;
        Unloaded += OnUnloaded;
    }

    public bool UseExpandedCalendarLayout
    {
        get => (bool)GetValue(UseExpandedCalendarLayoutProperty);
        private set => SetValue(UseExpandedCalendarLayoutProperty, value);
    }

    public async Task PrepareForOpenAsync(CancellationToken cancellationToken = default)
    {
        await _openPreparationGate.WaitAsync(cancellationToken);
        try
        {
            await _viewModel.LoadAsync(cancellationToken);
        }
        finally
        {
            _openPreparationGate.Release();
        }
    }

    private void OnCalendarMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        _viewModel.ScrollCalendarRowsCommand.Execute(e.Delta < 0 ? 1 : -1);
        e.Handled = true;
    }

    private void OnCalendarLoaded(object sender, RoutedEventArgs e)
    {
        UpdateExpandedCalendarLayout();
    }

    private void OnCalendarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateExpandedCalendarLayout();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnCalendarLoaded;
        SizeChanged -= OnCalendarSizeChanged;
        Unloaded -= OnUnloaded;
        _viewModel.Dispose();
    }

    private void UpdateExpandedCalendarLayout()
    {
        UseExpandedCalendarLayout = GetIsWindowLayoutMaximized() || ActualWidth >= ExpandedLayoutMinWidth;
    }

    private bool GetIsWindowLayoutMaximized()
    {
        return Window.GetWindow(this) is MainWindow { IsWindowLayoutMaximized: true };
    }
}
