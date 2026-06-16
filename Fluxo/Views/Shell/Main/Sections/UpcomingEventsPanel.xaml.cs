using System.Windows;
using System.Windows.Controls;

namespace Fluxo.Views.Shell.Main.Sections;

public partial class UpcomingEventsPanel : UserControl
{
    private const double VisibleItemCount = 2d;
    private const double ItemGap = 10d;

    public static readonly DependencyProperty UpcomingEventItemHeightProperty =
        DependencyProperty.Register(
            nameof(UpcomingEventItemHeight),
            typeof(double),
            typeof(UpcomingEventsPanel),
            new PropertyMetadata(0d));

    public UpcomingEventsPanel()
    {
        InitializeComponent();
    }

    public double UpcomingEventItemHeight
    {
        get => (double)GetValue(UpcomingEventItemHeightProperty);
        set => SetValue(UpcomingEventItemHeightProperty, value);
    }

    private void OnUpcomingEventsScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpcomingEventItemHeight = Math.Max(0d, (UpcomingEventsScrollViewer.ActualHeight - ItemGap) / VisibleItemCount);
    }
}
