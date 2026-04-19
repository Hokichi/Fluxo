using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Fluxo.Views.CustomControls;

public class FadingScrollViewer : ScrollViewer
{
    private const string PART_SCROLL_PRESENTER_CONTAINER_NAME = "PART_ScrollContentPresenterContainer";

    public static readonly DependencyProperty FadedEdgeThicknessProperty =
        DependencyProperty.Register(
            nameof(FadedEdgeThickness),
            typeof(double),
            typeof(FadingScrollViewer),
            new PropertyMetadata(20.0, OnFadedEdgeAppearancePropertyChanged));

    public static readonly DependencyProperty FadedEdgeFalloffSpeedProperty =
        DependencyProperty.Register(
            nameof(FadedEdgeFalloffSpeed),
            typeof(double),
            typeof(FadingScrollViewer),
            new PropertyMetadata(4.0, OnFadedEdgeAppearancePropertyChanged));

    public static readonly DependencyProperty FadedEdgeOpacityProperty =
        DependencyProperty.Register(
            nameof(FadedEdgeOpacity),
            typeof(double),
            typeof(FadingScrollViewer),
            new PropertyMetadata(0.0, OnFadedEdgeAppearancePropertyChanged));

    public FadingScrollViewer()
    {
        ScrollChanged += FadingScrollViewer_ScrollChanged;
        SizeChanged += FadingScrollViewer_SizeChanged;
    }

    public double FadedEdgeThickness
    {
        get => (double)GetValue(FadedEdgeThicknessProperty);
        set => SetValue(FadedEdgeThicknessProperty, value);
    }

    public double FadedEdgeFalloffSpeed
    {
        get => (double)GetValue(FadedEdgeFalloffSpeedProperty);
        set => SetValue(FadedEdgeFalloffSpeedProperty, value);
    }

    public double FadedEdgeOpacity
    {
        get => (double)GetValue(FadedEdgeOpacityProperty);
        set => SetValue(FadedEdgeOpacityProperty, value);
    }

    private BlurEffect InnerFadedBorderEffect { get; set; }
    private Border InnerFadedBorder { get; set; }
    private Border OuterFadedBorder { get; set; }

    private static void OnFadedEdgeAppearancePropertyChanged(DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FadingScrollViewer fadingScrollViewer)
            return;

        fadingScrollViewer.RefreshFadedMask();
    }

    private void FadingScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (InnerFadedBorder == null)
            return;

        var topOffset = CalculateNewMarginBasedOnOffsetFromEdge(VerticalOffset);
        ;
        var bottomOffset = CalculateNewMarginBasedOnOffsetFromEdge(ScrollableHeight - VerticalOffset);
        var leftOffset = CalculateNewMarginBasedOnOffsetFromEdge(HorizontalOffset);
        var rightOffset = CalculateNewMarginBasedOnOffsetFromEdge(ScrollableWidth - HorizontalOffset);

        InnerFadedBorder.Margin = new Thickness(leftOffset, topOffset, rightOffset, bottomOffset);
    }

    private double CalculateNewMarginBasedOnOffsetFromEdge(double edgeOffset)
    {
        var innerFadedBorderBaseMarginThickness = FadedEdgeThickness / 2.0;
        var calculatedOffset = innerFadedBorderBaseMarginThickness -
                               1.5 * (FadedEdgeThickness - edgeOffset / FadedEdgeFalloffSpeed);

        return Math.Min(innerFadedBorderBaseMarginThickness, calculatedOffset);
    }

    private void FadingScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (OuterFadedBorder == null || InnerFadedBorder == null || InnerFadedBorderEffect == null)
            return;

        OuterFadedBorder.Width = e.NewSize.Width;
        OuterFadedBorder.Height = e.NewSize.Height;

        var innerFadedBorderBaseMarginThickness = FadedEdgeThickness / 2.0;
        InnerFadedBorder.Margin = new Thickness(innerFadedBorderBaseMarginThickness);
        InnerFadedBorderEffect.Radius = FadedEdgeThickness;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        BuildInnerFadedBorderEffectForOpacityMask();
        BuildInnerFadedBorderForOpacityMask();
        BuildOuterFadedBorderForOpacityMask();
        SetOpacityMaskOfScrollContainer();
        RefreshFadedMask();
    }

    private void RefreshFadedMask()
    {
        if (OuterFadedBorder == null || InnerFadedBorder == null || InnerFadedBorderEffect == null)
            return;

        var fadedEdgeByteOpacity = (byte)(FadedEdgeOpacity * 255);
        OuterFadedBorder.Background = new SolidColorBrush(Color.FromArgb(fadedEdgeByteOpacity, 0, 0, 0));

        var innerFadedBorderBaseMarginThickness = FadedEdgeThickness / 2.0;
        InnerFadedBorder.Margin = new Thickness(innerFadedBorderBaseMarginThickness);
        InnerFadedBorderEffect.Radius = FadedEdgeThickness;
    }

    private void BuildInnerFadedBorderEffectForOpacityMask()
    {
        InnerFadedBorderEffect = new BlurEffect
        {
            RenderingBias = RenderingBias.Performance
        };
    }

    private void BuildInnerFadedBorderForOpacityMask()
    {
        InnerFadedBorder = new Border
        {
            Background = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Effect = InnerFadedBorderEffect
        };
    }

    private void BuildOuterFadedBorderForOpacityMask()
    {
        OuterFadedBorder = new Border
        {
            Background = Brushes.Transparent,
            ClipToBounds = true,
            Child = InnerFadedBorder
        };
    }

    private void SetOpacityMaskOfScrollContainer()
    {
        var opacityMaskBrush = new VisualBrush
        {
            Visual = OuterFadedBorder
        };

        var scrollContentPresentationContainer =
            Template.FindName(PART_SCROLL_PRESENTER_CONTAINER_NAME, this) as UIElement;

        if (scrollContentPresentationContainer == null)
            return;

        scrollContentPresentationContainer.OpacityMask = opacityMaskBrush;
    }
}