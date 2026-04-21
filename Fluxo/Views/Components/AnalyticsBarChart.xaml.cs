using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Fluxo.Views.Components;

public partial class AnalyticsBarChart : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(AnalyticsBarChart),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private bool _isInitialized;
    private bool _isLayerAActive = true;
    private int _transitionToken;

    public AnalyticsBarChart()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var chart = (AnalyticsBarChart)dependencyObject;
        chart.ApplyItems(eventArgs.NewValue as IEnumerable);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized)
            return;

        _isInitialized = true;
        SetActiveLayerItems(ItemsSource);
    }

    private void ApplyItems(IEnumerable? nextItems)
    {
        if (!_isInitialized)
            return;

        var activeLayer = _isLayerAActive ? LayerA : LayerB;
        var incomingLayer = _isLayerAActive ? LayerB : LayerA;
        _transitionToken++;
        var transitionToken = _transitionToken;

        incomingLayer.ItemsSource = nextItems;
        incomingLayer.Opacity = 0d;

        var fadeIn = new DoubleAnimation
        {
            From = 0d,
            To = 1d,
            Duration = TimeSpan.FromMilliseconds(180)
        };

        var fadeOut = new DoubleAnimation
        {
            From = 1d,
            To = 0d,
            Duration = TimeSpan.FromMilliseconds(180)
        };

        fadeOut.Completed += (_, _) =>
        {
            if (transitionToken != _transitionToken)
                return;

            activeLayer.ItemsSource = null;
            activeLayer.Opacity = 0d;
            _isLayerAActive = !_isLayerAActive;
        };

        incomingLayer.BeginAnimation(OpacityProperty, fadeIn);
        activeLayer.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void SetActiveLayerItems(IEnumerable? items)
    {
        if (_isLayerAActive)
        {
            LayerA.ItemsSource = items;
            LayerA.Opacity = 1d;
            LayerB.ItemsSource = null;
            LayerB.Opacity = 0d;
            return;
        }

        LayerB.ItemsSource = items;
        LayerB.Opacity = 1d;
        LayerA.ItemsSource = null;
        LayerA.Opacity = 0d;
    }
}
