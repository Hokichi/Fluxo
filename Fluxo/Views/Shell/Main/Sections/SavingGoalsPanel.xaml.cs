using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;

namespace Fluxo.Views.Shell.Main.Sections;

public partial class SavingGoalsPanel : UserControl
{
    private bool _isAnimating;
    private SavingGoalVM? _displayedGoal;
    private SavingGoalsPanelVM? _viewModel;

    public SavingGoalsPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            AttachViewModel(DataContext as SavingGoalsPanelVM);

        SyncCurrentGoalWithoutAnimation();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachViewModel();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ReferenceEquals(e.OldValue, e.NewValue))
            return;

        DetachViewModel();
        AttachViewModel(e.NewValue as SavingGoalsPanelVM);
        SyncCurrentGoalWithoutAnimation();
    }

    private void AttachViewModel(SavingGoalsPanelVM? viewModel)
    {
        _viewModel = viewModel;
        if (_viewModel is null)
            return;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void DetachViewModel()
    {
        if (_viewModel is null)
            return;

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
            return;

        if (e.PropertyName is nameof(SavingGoalsPanelVM.CurrentGoal) or nameof(SavingGoalsPanelVM.HasSavingGoals))
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(SyncOrAnimateCurrentGoal);
                return;
            }

            SyncOrAnimateCurrentGoal();
        }
    }

    private void OnNavigatePreviousClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _isAnimating || !_viewModel.HasMultipleSavingGoals)
            return;

        _viewModel.NavigatePrevious();
    }

    private void OnNavigateNextClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _isAnimating || !_viewModel.HasMultipleSavingGoals)
            return;

        _viewModel.NavigateNext();
    }

    private void SyncOrAnimateCurrentGoal()
    {
        if (_viewModel is null)
        {
            SyncCurrentGoalWithoutAnimation();
            return;
        }

        var incomingGoal = _viewModel.CurrentGoal;

        if (_isAnimating ||
            _displayedGoal is null ||
            incomingGoal is null ||
            ReferenceEquals(_displayedGoal, incomingGoal) ||
            _viewModel.NavigationDirection == 0 ||
            !_viewModel.HasMultipleSavingGoals)
        {
            SyncCurrentGoalWithoutAnimation();
            return;
        }

        RunSlideAnimation(_displayedGoal, incomingGoal, _viewModel.NavigationDirection);
    }

    private void SyncCurrentGoalWithoutAnimation()
    {
        _isAnimating = false;
        _displayedGoal = _viewModel?.CurrentGoal;
        CurrentGoalPresenter.Content = _displayedGoal;

        var currentTransform = EnsureTranslateTransform(CurrentGoalPresenter);
        currentTransform.X = 0;

        var incomingTransform = EnsureTranslateTransform(IncomingGoalPresenter);
        incomingTransform.X = 0;

        IncomingGoalPresenter.Content = null;
        IncomingGoalPresenter.Visibility = Visibility.Collapsed;
    }

    private void RunSlideAnimation(SavingGoalVM outgoingGoal, SavingGoalVM incomingGoal, int direction)
    {
        var animationWidth = CurrentGoalPresenter.ActualWidth;
        if (animationWidth <= 0)
            animationWidth = ActualWidth > 0 ? ActualWidth : 280;

        var outgoingTransform = EnsureTranslateTransform(CurrentGoalPresenter);
        var incomingTransform = EnsureTranslateTransform(IncomingGoalPresenter);

        var distance = animationWidth;
        var outgoingTarget = distance * direction;
        var incomingStart = -distance * direction;

        _isAnimating = true;

        CurrentGoalPresenter.Content = outgoingGoal;
        outgoingTransform.X = 0;

        IncomingGoalPresenter.Content = incomingGoal;
        IncomingGoalPresenter.Visibility = Visibility.Visible;
        incomingTransform.X = incomingStart;

        var duration = TimeSpan.FromMilliseconds(220);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        var outgoingAnimation = new DoubleAnimation
        {
            To = outgoingTarget,
            Duration = duration,
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        };

        var incomingAnimation = new DoubleAnimation
        {
            To = 0,
            Duration = duration,
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        };

        outgoingAnimation.Completed += (_, _) =>
        {
            _isAnimating = false;
            _displayedGoal = incomingGoal;

            outgoingTransform.X = 0;
            incomingTransform.X = 0;

            CurrentGoalPresenter.Content = incomingGoal;
            IncomingGoalPresenter.Content = null;
            IncomingGoalPresenter.Visibility = Visibility.Collapsed;
        };

        outgoingTransform.BeginAnimation(TranslateTransform.XProperty, outgoingAnimation);
        incomingTransform.BeginAnimation(TranslateTransform.XProperty, incomingAnimation);
    }

    private static TranslateTransform EnsureTranslateTransform(UIElement element)
    {
        if (element.RenderTransform is TranslateTransform transform)
            return transform;

        var createdTransform = new TranslateTransform();
        element.RenderTransform = createdTransform;
        return createdTransform;
    }
}
