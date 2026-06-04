using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Fluxo.Resources.CustomControls;

[TemplatePart(Name = PartShape, Type = typeof(Path))]
[TemplatePart(Name = PartOverlay, Type = typeof(Path))]
[TemplatePart(Name = PartIcon, Type = typeof(Path))]
[TemplatePart(Name = PartTextReveal, Type = typeof(Border))]
[TemplatePart(Name = PartButtonText, Type = typeof(TextBlock))]
public class BalloonButton : Button
{
    private const string PartShape = "PART_Shape";
    private const string PartOverlay = "PART_Overlay";
    private const string PartIcon = "PART_Icon";
    private const string PartTextReveal = "PART_TextReveal";
    private const string PartButtonText = "PART_ButtonText";

    private static readonly Duration FadeDuration = new(TimeSpan.FromSeconds(0.2));
    private static readonly Duration ExpandDuration = new(TimeSpan.FromSeconds(0.18));
    private static readonly Duration CollapseDuration = new(TimeSpan.FromSeconds(0.16));
    private static readonly Duration TextFadeDuration = new(TimeSpan.FromSeconds(0.12));

    // --- DefaultBackground ---
    public static readonly DependencyProperty DefaultBackgroundProperty =
        DependencyProperty.Register(nameof(DefaultBackground), typeof(Brush), typeof(BalloonButton),
            new PropertyMetadata(Brushes.CornflowerBlue, (d, _) => ((BalloonButton)d).ResetShapeFill()));

    // --- HoveredBackground ---
    public static readonly DependencyProperty HoveredBackgroundProperty =
        DependencyProperty.Register(nameof(HoveredBackground), typeof(Brush), typeof(BalloonButton),
            new PropertyMetadata(Brushes.RoyalBlue));

    // --- ButtonIcon ---
    public static readonly DependencyProperty ButtonIconProperty =
        DependencyProperty.Register(nameof(ButtonIcon), typeof(object), typeof(BalloonButton),
            new PropertyMetadata(null, (d, _) => ((BalloonButton)d).ApplyIcon()));

    // --- ButtonText ---
    public static readonly DependencyProperty ButtonTextProperty =
        DependencyProperty.Register(nameof(ButtonText), typeof(string), typeof(BalloonButton),
            new PropertyMetadata(null));

    // --- ShouldExpand ---
    public static readonly DependencyProperty ShouldExpandProperty =
        DependencyProperty.Register(nameof(ShouldExpand), typeof(bool), typeof(BalloonButton),
            new PropertyMetadata(false));

    // --- ButtonSize ---
    public static readonly DependencyProperty ButtonSizeProperty =
        DependencyProperty.Register(nameof(ButtonSize), typeof(double), typeof(BalloonButton),
            new PropertyMetadata(30.0));

    // --- ExpandedWidth ---
    public static readonly DependencyProperty ExpandedWidthProperty =
        DependencyProperty.Register(nameof(ExpandedWidth), typeof(double), typeof(BalloonButton),
            new PropertyMetadata(96.0));

    // --- CurveHeight ---
    public static readonly DependencyProperty CurveHeightProperty =
        DependencyProperty.Register(nameof(CurveHeight), typeof(double), typeof(BalloonButton),
            new FrameworkPropertyMetadata(13.0, FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryInvalidated));

    // --- CornerRadius ---
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(double), typeof(BalloonButton),
            new FrameworkPropertyMetadata(15.0, FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryInvalidated));

    // --- IconSize ---
    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(BalloonButton),
            new PropertyMetadata(24.0));

    private Path? _icon;
    private Path? _overlay;

    private Path? _shape;
    private TextBlock? _buttonText;
    private Border? _textReveal;

    static BalloonButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(BalloonButton),
            new FrameworkPropertyMetadata(typeof(BalloonButton)));
    }

    public Brush DefaultBackground
    {
        get => (Brush)GetValue(DefaultBackgroundProperty);
        set => SetValue(DefaultBackgroundProperty, value);
    }

    public Brush HoveredBackground
    {
        get => (Brush)GetValue(HoveredBackgroundProperty);
        set => SetValue(HoveredBackgroundProperty, value);
    }

    public object? ButtonIcon
    {
        get => GetValue(ButtonIconProperty);
        set => SetValue(ButtonIconProperty, value);
    }

    public string? ButtonText
    {
        get => (string?)GetValue(ButtonTextProperty);
        set => SetValue(ButtonTextProperty, value);
    }

    public bool ShouldExpand
    {
        get => (bool)GetValue(ShouldExpandProperty);
        set => SetValue(ShouldExpandProperty, value);
    }

    public double ButtonSize
    {
        get => (double)GetValue(ButtonSizeProperty);
        set => SetValue(ButtonSizeProperty, value);
    }

    public double ExpandedWidth
    {
        get => (double)GetValue(ExpandedWidthProperty);
        set => SetValue(ExpandedWidthProperty, value);
    }

    public double CurveHeight
    {
        get => (double)GetValue(CurveHeightProperty);
        set => SetValue(CurveHeightProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    // -------------------------------------------------------

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _shape = GetTemplateChild(PartShape) as Path;
        _overlay = GetTemplateChild(PartOverlay) as Path;
        _icon = GetTemplateChild(PartIcon) as Path;
        _textReveal = GetTemplateChild(PartTextReveal) as Border;
        _buttonText = GetTemplateChild(PartButtonText) as TextBlock;
        ResetShapeFill();
        ApplyIcon();
        ResetExpansion();
        RebuildGeometry();
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        AnimateShapeFill(HoveredBackground);
        AnimateExpansion(isExpanded: true);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        AnimateShapeFill(DefaultBackground);
        AnimateExpansion(isExpanded: false);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        RebuildGeometry();
    }

    private static void OnGeometryInvalidated(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((BalloonButton)d).RebuildGeometry();
    }

    // Snaps the fill back to DefaultBackground (no animation); called on template apply or DP change.
    private void ResetShapeFill()
    {
        if (_shape == null) return;
        if (DefaultBackground is SolidColorBrush scb)
            _shape.Fill = new SolidColorBrush(scb.Color);
        else
            _shape.Fill = DefaultBackground;
    }

    private void AnimateShapeFill(Brush target)
    {
        if (_shape?.Fill is not SolidColorBrush current) return;
        if (target is not SolidColorBrush targetScb) return;

        var animation = new ColorAnimation(targetScb.Color, FadeDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        current.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    private void ResetExpansion()
    {
        Width = ButtonSize;

        if (_textReveal is not null)
        {
            _textReveal.Width = 0;
            _textReveal.Opacity = 0;
        }

        if (_buttonText is not null)
            _buttonText.Opacity = 0;
    }

    private void AnimateExpansion(bool isExpanded)
    {
        if (!ShouldExpand)
            return;

        var widthTarget = isExpanded ? ExpandedWidth : ButtonSize;
        var textWidthTarget = isExpanded ? Math.Max(0, ExpandedWidth - ButtonSize - 8) : 0;
        var opacityTarget = isExpanded ? 1 : 0;
        var duration = isExpanded ? ExpandDuration : CollapseDuration;
        var easingMode = isExpanded ? EasingMode.EaseOut : EasingMode.EaseIn;
        var easing = new CubicEase { EasingMode = easingMode };

        BeginAnimation(WidthProperty, new DoubleAnimation(widthTarget, duration)
        {
            EasingFunction = easing
        });

        if (_textReveal is not null)
        {
            _textReveal.BeginAnimation(WidthProperty, new DoubleAnimation(textWidthTarget, duration)
            {
                EasingFunction = easing
            });
            _textReveal.BeginAnimation(OpacityProperty, new DoubleAnimation(opacityTarget, TextFadeDuration));
        }

        if (_buttonText is not null)
            _buttonText.BeginAnimation(OpacityProperty, new DoubleAnimation(opacityTarget, TextFadeDuration));
    }

    private void ApplyIcon()
    {
        if (_icon is not null)
            _icon.Data = ResolveGeometry(ButtonIcon);
    }

    private static Geometry? ResolveGeometry(object? source)
    {
        switch (source)
        {
            case Geometry geometry:
                return geometry;
            case GeometryDrawing geometryDrawing:
                return geometryDrawing.Geometry;
            case DrawingGroup drawingGroup:
                foreach (var drawing in drawingGroup.Children)
                {
                    var resolved = ResolveGeometry(drawing);
                    if (resolved is not null)
                        return resolved;
                }

                break;
        }

        return null;
    }

    private void RebuildGeometry()
    {
        if (_shape == null || _overlay == null) return;

        var w = ActualWidth;
        var h = ActualHeight;
        var c = CurveHeight;
        var r = CornerRadius;

        if (w <= 0 || h <= 0) return;

        var figure = new PathFigure { StartPoint = new Point(r, 0), IsClosed = true };

        figure.Segments.Add(new BezierSegment(new Point(w * 0.25, -c), new Point(w * 0.75, -c), new Point(w - r, 0),
            true));
        figure.Segments.Add(new ArcSegment(new Point(w, r), new Size(r, r), 0, false, SweepDirection.Clockwise, true));
        figure.Segments.Add(new BezierSegment(new Point(w + c, h * 0.25), new Point(w + c, h * 0.75),
            new Point(w, h - r), true));
        figure.Segments.Add(new ArcSegment(new Point(w - r, h), new Size(r, r), 0, false, SweepDirection.Clockwise,
            true));
        figure.Segments.Add(new BezierSegment(new Point(w * 0.75, h + c), new Point(w * 0.25, h + c), new Point(r, h),
            true));
        figure.Segments.Add(new ArcSegment(new Point(0, h - r), new Size(r, r), 0, false, SweepDirection.Clockwise,
            true));
        figure.Segments.Add(new BezierSegment(new Point(-c, h * 0.75), new Point(-c, h * 0.25), new Point(0, r), true));
        figure.Segments.Add(new ArcSegment(new Point(r, 0), new Size(r, r), 0, false, SweepDirection.Clockwise, true));

        var geometry = new PathGeometry(new[] { figure });
        _shape.Data = geometry;
        _overlay.Data = geometry;
    }
}
