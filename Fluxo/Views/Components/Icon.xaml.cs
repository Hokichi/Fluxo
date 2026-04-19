using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Fluxo.Views.Components;

public partial class Icon : UserControl
{
    public static readonly DependencyProperty PathProperty = DependencyProperty.Register(
        nameof(Path),
        typeof(object),
        typeof(Icon),
        new PropertyMetadata(null, OnPathChanged));

    public static readonly DependencyProperty MaskBrushProperty = DependencyProperty.Register(
        nameof(MaskBrush),
        typeof(Brush),
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

    public Brush? MaskBrush
    {
        get => (Brush?)GetValue(MaskBrushProperty);
        private set => SetValue(MaskBrushProperty, value);
    }

    public Brush? Color
    {
        get => (Brush?)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    private static void OnPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((Icon)d).UpdateMaskBrush();
    }

    private void UpdateMaskBrush()
    {
        var geometry = ResolveGeometry(Path);
        if (geometry is not null)
        {
            MaskBrush = new DrawingBrush(new GeometryDrawing(Brushes.Black, null, geometry))
            {
                Stretch = Stretch.Uniform
            };
            return;
        }

        var imageSource = ResolveImageSource(Path);
        if (imageSource is not null)
        {
            MaskBrush = new ImageBrush(imageSource)
            {
                Stretch = Stretch.Uniform
            };
            return;
        }

        MaskBrush = null;
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

    private static ImageSource? ResolveImageSource(object? source)
    {
        switch (source)
        {
            case ImageSource imageSource:
                return imageSource;
            case Uri uri:
                return new BitmapImage(uri);
            case string path when !string.IsNullOrWhiteSpace(path):
                try
                {
                    var converter = new ImageSourceConverter();
                    return converter.ConvertFromInvariantString(path) as ImageSource;
                }
                catch
                {
                    return null;
                }
        }

        return null;
    }
}
