using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Fluxo.ViewModels.Shell.Main;
using NotificationPanelVM = Fluxo.ViewModels.Shell.Main.NotificationPanelVM;

namespace Fluxo.Views.Shell.Main.Sections;

public partial class NotificationPanel : UserControl
{
    private const double SwipeDistanceThreshold = 48;
    private static readonly TimeSpan SwipeTransitionDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan EmptyStateFadeDuration = TimeSpan.FromMilliseconds(120);
    private static readonly IEasingFunction SwipeTransitionEasing = new CubicEase { EasingMode = EasingMode.EaseOut };

    private bool _isAnimating;
    private bool _isSwipeTracking;
    private Point _swipeStartPoint;
    private NotificationItemVM? _displayedNotification;
    private NotificationPanelVM? _viewModel;

    public NotificationPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnsureTransforms();

        if (_viewModel is null)
            AttachViewModel(DataContext as NotificationPanelVM);

        SyncCurrentNotificationWithoutAnimation();
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
        AttachViewModel(e.NewValue as NotificationPanelVM);
        SyncCurrentNotificationWithoutAnimation();
    }

    private void AttachViewModel(NotificationPanelVM? viewModel)
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

        if (e.PropertyName is nameof(NotificationPanelVM.CurrentNotificationItem) or nameof(NotificationPanelVM.HasNotifications))
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(SyncOrAnimateCurrentNotification);
                return;
            }

            SyncOrAnimateCurrentNotification();
        }
    }

    private void OnNavigatePreviousClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _isAnimating || !_viewModel.HasMultipleNotifications)
            return;

        _viewModel.NavigatePreviousCommand.Execute(null);
    }

    private void OnNavigateNextClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _isAnimating || !_viewModel.HasMultipleNotifications)
            return;

        _viewModel.NavigateNextCommand.Execute(null);
    }

    private void OnCarouselViewportPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_viewModel is null || _isAnimating || !_viewModel.HasMultipleNotifications)
            return;

        _isSwipeTracking = true;
        _swipeStartPoint = e.GetPosition(CarouselViewport);
        CarouselViewport.CaptureMouse();
    }

    private void OnCarouselViewportPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isSwipeTracking || _viewModel is null || _isAnimating || !_viewModel.HasMultipleNotifications)
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

    private void SyncOrAnimateCurrentNotification()
    {
        if (_viewModel is null)
        {
            SyncCurrentNotificationWithoutAnimation();
            return;
        }

        var incomingNotification = _viewModel.CurrentNotificationItem;
        if (_isAnimating)
            return;

        if (_displayedNotification is not null &&
            incomingNotification is not null &&
            !ReferenceEquals(_displayedNotification, incomingNotification) &&
            _viewModel.NavigationDirection != 0 &&
            _viewModel.HasMultipleNotifications)
        {
            RunSwipeAnimation(_displayedNotification, incomingNotification, _viewModel.NavigationDirection);
            return;
        }

        if (_displayedNotification is not null && incomingNotification is null)
        {
            RunFadeToEmptyAnimation(_displayedNotification);
            return;
        }

        SyncCurrentNotificationWithoutAnimation();
    }

    private void SyncCurrentNotificationWithoutAnimation()
    {
        _isAnimating = false;
        EnsureTransforms();

        CurrentNotificationPresenter.BeginAnimation(OpacityProperty, null);
        IncomingNotificationPresenter.BeginAnimation(OpacityProperty, null);
        EmptyStateText.BeginAnimation(OpacityProperty, null);

        CurrentNotificationPresenter.RenderTransform.BeginAnimation(TranslateTransform.XProperty, null);
        IncomingNotificationPresenter.RenderTransform.BeginAnimation(TranslateTransform.XProperty, null);

        CurrentNotificationPresenter.Opacity = 1;
        IncomingNotificationPresenter.Opacity = 1;
        EmptyStateText.Opacity = 0;

        IncomingNotificationPresenter.Content = null;
        IncomingNotificationPresenter.Visibility = Visibility.Collapsed;

        _displayedNotification = _viewModel?.CurrentNotificationItem;
        CurrentNotificationPresenter.Content = _displayedNotification;

        if (_displayedNotification is null)
        {
            CurrentNotificationPresenter.Visibility = Visibility.Collapsed;
            EmptyStateText.Visibility = Visibility.Visible;
            EmptyStateText.Opacity = 1;
            return;
        }

        CurrentNotificationPresenter.Visibility = Visibility.Visible;
        EmptyStateText.Visibility = Visibility.Collapsed;
    }

    private void RunSwipeAnimation(NotificationItemVM outgoingNotification, NotificationItemVM incomingNotification, int navigationDirection)
    {
        _isAnimating = true;
        EnsureTransforms();

        var viewportWidth = Math.Max(CarouselViewport.ActualWidth, 240d);
        var outgoingTransform = (TranslateTransform)CurrentNotificationPresenter.RenderTransform;
        var incomingTransform = (TranslateTransform)IncomingNotificationPresenter.RenderTransform;
        var incomingFrom = navigationDirection < 0 ? viewportWidth : -viewportWidth;
        var outgoingTo = navigationDirection < 0 ? -viewportWidth : viewportWidth;

        CurrentNotificationPresenter.Visibility = Visibility.Visible;
        CurrentNotificationPresenter.Content = outgoingNotification;
        CurrentNotificationPresenter.Opacity = 1;
        outgoingTransform.X = 0;

        EmptyStateText.Visibility = Visibility.Collapsed;
        EmptyStateText.Opacity = 0;

        IncomingNotificationPresenter.Visibility = Visibility.Visible;
        IncomingNotificationPresenter.Content = incomingNotification;
        IncomingNotificationPresenter.Opacity = 1;
        incomingTransform.X = incomingFrom;

        var outgoingSlide = new DoubleAnimation
        {
            From = 0,
            To = outgoingTo,
            Duration = SwipeTransitionDuration,
            EasingFunction = SwipeTransitionEasing,
            FillBehavior = FillBehavior.Stop
        };

        var incomingSlide = new DoubleAnimation
        {
            From = incomingFrom,
            To = 0,
            Duration = SwipeTransitionDuration,
            EasingFunction = SwipeTransitionEasing,
            FillBehavior = FillBehavior.Stop
        };

        outgoingSlide.Completed += (_, _) =>
        {
            _isAnimating = false;
            _displayedNotification = incomingNotification;

            outgoingTransform.BeginAnimation(TranslateTransform.XProperty, null);
            incomingTransform.BeginAnimation(TranslateTransform.XProperty, null);

            CurrentNotificationPresenter.Visibility = Visibility.Visible;
            CurrentNotificationPresenter.Content = incomingNotification;
            CurrentNotificationPresenter.Opacity = 1;
            outgoingTransform.X = 0;

            IncomingNotificationPresenter.Content = null;
            IncomingNotificationPresenter.Visibility = Visibility.Collapsed;
            incomingTransform.X = 0;
        };

        outgoingTransform.BeginAnimation(TranslateTransform.XProperty, outgoingSlide);
        incomingTransform.BeginAnimation(TranslateTransform.XProperty, incomingSlide);
    }

    private void RunFadeToEmptyAnimation(NotificationItemVM outgoingNotification)
    {
        _isAnimating = true;
        EnsureTransforms();

        CurrentNotificationPresenter.Content = outgoingNotification;
        CurrentNotificationPresenter.Visibility = Visibility.Visible;
        CurrentNotificationPresenter.Opacity = 1;
        ((TranslateTransform)CurrentNotificationPresenter.RenderTransform).X = 0;

        IncomingNotificationPresenter.Content = null;
        IncomingNotificationPresenter.Visibility = Visibility.Collapsed;
        EmptyStateText.Visibility = Visibility.Visible;
        EmptyStateText.Opacity = 0;

        var outgoingFade = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = EmptyStateFadeDuration,
            EasingFunction = SwipeTransitionEasing,
            FillBehavior = FillBehavior.Stop
        };

        var emptyFade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = EmptyStateFadeDuration,
            EasingFunction = SwipeTransitionEasing,
            FillBehavior = FillBehavior.Stop
        };

        outgoingFade.Completed += (_, _) =>
        {
            _isAnimating = false;
            _displayedNotification = null;

            CurrentNotificationPresenter.BeginAnimation(OpacityProperty, null);
            EmptyStateText.BeginAnimation(OpacityProperty, null);

            CurrentNotificationPresenter.Content = null;
            CurrentNotificationPresenter.Visibility = Visibility.Collapsed;
            CurrentNotificationPresenter.Opacity = 1;

            EmptyStateText.Visibility = Visibility.Visible;
            EmptyStateText.Opacity = 1;
        };

        CurrentNotificationPresenter.BeginAnimation(OpacityProperty, outgoingFade);
        EmptyStateText.BeginAnimation(OpacityProperty, emptyFade);
    }

    private void CompleteSwipeTracking(Point endPoint, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isSwipeTracking = false;

        if (CarouselViewport.IsMouseCaptured)
            CarouselViewport.ReleaseMouseCapture();

        if (_viewModel is null || _isAnimating || !_viewModel.HasMultipleNotifications)
            return;

        var horizontalDistance = endPoint.X - _swipeStartPoint.X;
        var verticalDistance = endPoint.Y - _swipeStartPoint.Y;
        var absHorizontalDistance = Math.Abs(horizontalDistance);
        var absVerticalDistance = Math.Abs(verticalDistance);

        if (absHorizontalDistance < SwipeDistanceThreshold || absHorizontalDistance <= absVerticalDistance)
            return;

        if (horizontalDistance > 0)
            _viewModel.NavigatePreviousCommand.Execute(null);
        else
            _viewModel.NavigateNextCommand.Execute(null);

        e.Handled = true;
    }

    private void CancelSwipeTracking()
    {
        _isSwipeTracking = false;

        if (CarouselViewport.IsMouseCaptured)
            CarouselViewport.ReleaseMouseCapture();
    }

    private void EnsureTransforms()
    {
        if (CurrentNotificationPresenter.RenderTransform is not TranslateTransform)
            CurrentNotificationPresenter.RenderTransform = new TranslateTransform();

        if (IncomingNotificationPresenter.RenderTransform is not TranslateTransform)
            IncomingNotificationPresenter.RenderTransform = new TranslateTransform();
    }
}
