using System.Windows;
using System.Windows.Controls;

namespace Fluxo.Resources.CustomControls;

public class CollapsingCard : ContentControl
{
    static CollapsingCard()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(CollapsingCard),
            new FrameworkPropertyMetadata(typeof(CollapsingCard)));
    }

    public static readonly DependencyProperty HeaderTextProperty =
        DependencyProperty.Register(
            nameof(HeaderText),
            typeof(string),
            typeof(CollapsingCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SubheaderAreaProperty =
        DependencyProperty.Register(
            nameof(SubheaderArea),
            typeof(object),
            typeof(CollapsingCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(
            nameof(IsExpanded),
            typeof(bool),
            typeof(CollapsingCard),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string? HeaderText
    {
        get => (string?)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public object? SubheaderArea
    {
        get => GetValue(SubheaderAreaProperty);
        set => SetValue(SubheaderAreaProperty, value);
    }

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }
}
