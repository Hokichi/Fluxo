using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Fluxo.ViewModels.CustomControls;

namespace Fluxo.Views.CustomControls;

public sealed class StepNavigatorControl : Control
{
    static StepNavigatorControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(StepNavigatorControl),
            new FrameworkPropertyMetadata(typeof(StepNavigatorControl)));
    }

    public static readonly DependencyProperty StepCountProperty =
        DependencyProperty.Register(
            nameof(StepCount),
            typeof(int),
            typeof(StepNavigatorControl),
            new PropertyMetadata(0, OnStepCountChanged));

    public static readonly DependencyProperty CurrentStepProperty =
        DependencyProperty.Register(
            nameof(CurrentStep),
            typeof(int),
            typeof(StepNavigatorControl),
            new PropertyMetadata(0, OnCurrentStepChanged));

    public static readonly DependencyProperty ShouldIndicateProgressProperty =
        DependencyProperty.Register(
            nameof(ShouldIndicateProgress),
            typeof(bool),
            typeof(StepNavigatorControl),
            new PropertyMetadata(true));

    private static readonly DependencyPropertyKey DotsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Dots),
            typeof(ObservableCollection<StepNavigatorDotVM>),
            typeof(StepNavigatorControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty DotsProperty = DotsPropertyKey.DependencyProperty;

    public StepNavigatorControl()
    {
        SetValue(DotsPropertyKey, new ObservableCollection<StepNavigatorDotVM>());
    }

    public int StepCount
    {
        get => (int)GetValue(StepCountProperty);
        set => SetValue(StepCountProperty, value);
    }

    public int CurrentStep
    {
        get => (int)GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    public bool ShouldIndicateProgress
    {
        get => (bool)GetValue(ShouldIndicateProgressProperty);
        set => SetValue(ShouldIndicateProgressProperty, value);
    }

    public ObservableCollection<StepNavigatorDotVM> Dots =>
        (ObservableCollection<StepNavigatorDotVM>)GetValue(DotsProperty);

    private static void OnStepCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (StepNavigatorControl)d;
        var dots = control.Dots;
        dots.Clear();

        var count = (int)e.NewValue;
        for (var i = 0; i < count; i++)
            dots.Add(new StepNavigatorDotVM { IsFirst = i == 0 });

        UpdateDotStates(dots, control.CurrentStep);
    }

    private static void OnCurrentStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (StepNavigatorControl)d;
        UpdateDotStates(control.Dots, (int)e.NewValue);
    }

    internal static void UpdateDotStates(ObservableCollection<StepNavigatorDotVM> dots, int currentStep)
    {
        for (var i = 0; i < dots.Count; i++)
        {
            dots[i].IsCompleted = i < currentStep - 1;
            dots[i].IsActive = i == currentStep - 1;
        }
    }
}
