using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;
using SavingGoalsPanelVM = Fluxo.ViewModels.Shell.Main.SavingGoalsPanelVM;

namespace Fluxo.Views.Shell.Main.Sections;

public partial class SavingGoalsPanel : UserControl
{
    private const double SwipeDistanceThreshold = 48;
    private bool _isAnimating;
    private bool _isSwipeTracking;
    private Point _swipeStartPoint;
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
        CancelSwipeTracking();
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

    private void OnAddSavingGoalClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is Fluxo.Views.Shell.Main.MainWindow mainWindow)
            mainWindow.OpenAddSavingGoalPopup();
    }

    private void OnCarouselViewportPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_viewModel is null || _isAnimating || !_viewModel.HasMultipleSavingGoals)
            return;

        _isSwipeTracking = true;
        _swipeStartPoint = e.GetPosition(CarouselViewport);
        CarouselViewport.CaptureMouse();
    }

    private void OnCarouselViewportPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isSwipeTracking || _viewModel is null || _isAnimating || !_viewModel.HasMultipleSavingGoals)
            return;

        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
            CancelSwipeTracking();
    }

    private void OnCarouselViewportPreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isSwipeTracking)
            return;

        var endPoint = e.GetPosition(CarouselViewport);
        CompleteSwipeTracking(endPoint, e);
    }

    private void OnCarouselViewportLostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isSwipeTracking = false;
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

    private void CompleteSwipeTracking(Point endPoint, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isSwipeTracking = false;

        if (CarouselViewport.IsMouseCaptured)
            CarouselViewport.ReleaseMouseCapture();

        if (_viewModel is null || _isAnimating || !_viewModel.HasMultipleSavingGoals)
            return;

        var horizontalDistance = endPoint.X - _swipeStartPoint.X;
        var verticalDistance = endPoint.Y - _swipeStartPoint.Y;
        var absHorizontalDistance = Math.Abs(horizontalDistance);
        var absVerticalDistance = Math.Abs(verticalDistance);

        if (absHorizontalDistance < SwipeDistanceThreshold || absHorizontalDistance <= absVerticalDistance)
            return;

        if (horizontalDistance > 0)
            _viewModel.NavigatePrevious();
        else
            _viewModel.NavigateNext();

        e.Handled = true;
    }

    private void CancelSwipeTracking()
    {
        _isSwipeTracking = false;

        if (CarouselViewport.IsMouseCaptured)
            CarouselViewport.ReleaseMouseCapture();
    }
}
