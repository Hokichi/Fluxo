using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Fluxo.Views.Components;

public partial class Icon : UserControl
{
    public static readonly DependencyProperty PathProperty = DependencyProperty.Register(
        nameof(Path),
        typeof(object),
        typeof(Icon),
        new PropertyMetadata(null, OnPathChanged));

    public static readonly DependencyProperty MaskDrawingProperty = DependencyProperty.Register(
        nameof(MaskDrawing),
        typeof(Drawing),
        typeof(Icon),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color),
        typeof(Brush),
        typeof(Icon),
        new PropertyMetadata(Brushes.Black));

    public Icon()
    {
        InitializeComponent();
    }

    public object? Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    public Drawing? MaskDrawing
    {
        get => (Drawing?)GetValue(MaskDrawingProperty);
        private set => SetValue(MaskDrawingProperty, value);
    }

    public Brush? Color
    {
        get => (Brush?)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    private static void OnPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((Icon)d).UpdateMaskDrawing();
    }

    private void UpdateMaskDrawing()
    {
        var geometry = ResolveGeometry(Path);
        MaskDrawing = geometry is null ? null : new GeometryDrawing(Brushes.Black, null, geometry);
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
}
