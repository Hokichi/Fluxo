using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace Fluxo.Resources.CustomControls;

[TemplatePart(Name = PartStatePopup, Type = typeof(Popup))]
public class BalloonToggle : BalloonControl
{
    private const string PartStatePopup = "PART_StatePopup";
    internal static readonly TimeSpan LongPressDuration = TimeSpan.FromMilliseconds(300);

    private static readonly DependencyPropertyKey IsCyclingPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsCycling), typeof(bool), typeof(BalloonToggle),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsCyclingProperty = IsCyclingPropertyKey.DependencyProperty;

    public static readonly DependencyProperty StatesProperty =
        DependencyProperty.Register(nameof(States), typeof(FreezableCollection<BalloonToggleState>),
            typeof(BalloonToggle));

    private BalloonToggleState? _activeState;
    private BalloonToggleState[] _stateOrder = [];
    private readonly DispatcherTimer _longPressTimer;
    private Popup? _statePopup;
    private UIElement? _statePopupChild;
    private bool _suppressCycling;

    static BalloonToggle()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(BalloonToggle),
            new FrameworkPropertyMetadata(typeof(BalloonToggle)));
    }

    public BalloonToggle()
    {
        SetValue(StatesProperty, new FreezableCollection<BalloonToggleState>());
        States.Changed += OnStatesChanged;
        _longPressTimer = new DispatcherTimer { Interval = LongPressDuration };
        _longPressTimer.Tick += OnLongPressTimerTick;
        IsEnabledChanged += OnIsEnabledChanged;
    }

    public bool IsCycling => (bool)GetValue(IsCyclingProperty);

    public FreezableCollection<BalloonToggleState> States =>
        (FreezableCollection<BalloonToggleState>)GetValue(StatesProperty);

    public override void OnApplyTemplate()
    {
        DetachPopup();
        base.OnApplyTemplate();
        _statePopup = GetTemplateChild(PartStatePopup) as Popup;
        if (_statePopup is null)
            return;

        _statePopup.Closed += OnStatePopupClosed;
        _statePopupChild = _statePopup.Child;
        if (_statePopupChild is null)
            return;

        _statePopupChild.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnPopupButtonClick));
        _statePopupChild.PreviewKeyDown += OnPopupPreviewKeyDown;
    }

    protected override void OnClick()
    {
        if (_suppressCycling || _statePopup?.IsOpen == true)
            return;

        Cycle();
        base.OnClick();
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        if (IsEnabled && _statePopup?.IsOpen != true)
            _longPressTimer.Start();
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _longPressTimer.Stop();
        base.OnPreviewMouseLeftButtonUp(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _longPressTimer.Stop();
        base.OnMouseLeave(e);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        _longPressTimer.Stop();
        base.OnLostMouseCapture(e);
    }

    private void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue)
            _longPressTimer.Stop();
    }

    internal bool TryOpenStatePopup()
    {
        if (States.Count == 0)
            return false;

        _suppressCycling = true;
        if (_statePopup is not null)
        {
            _statePopup.IsOpen = true;
            Dispatcher.BeginInvoke(() => _statePopupChild?.MoveFocus(
                new TraversalRequest(FocusNavigationDirection.First)));
        }

        return true;
    }

    internal void SelectState(BalloonToggleState state)
    {
        if (!States.Contains(state))
            return;

        Activate(state);
        if (_statePopup is not null)
            _statePopup.IsOpen = false;
        else
            _suppressCycling = false;
    }

    private void Cycle()
    {
        if (States.Count == 0)
            return;

        if (!IsCycling)
        {
            Activate(States[0]);
            return;
        }

        var index = _activeState is null ? -1 : States.IndexOf(_activeState);
        if (index < 0 || index == States.Count - 1)
        {
            ResetCycle();
            return;
        }

        Activate(States[index + 1]);
    }

    private void Activate(BalloonToggleState state)
    {
        _activeState = state;
        SetValue(IsCyclingPropertyKey, true);
        ApplyState(state);
        if (state.OnChecked?.CanExecute(null) == true)
            state.OnChecked.Execute(null);
    }

    private void ResetCycle()
    {
        _activeState = null;
        SetValue(IsCyclingPropertyKey, false);
        if (States.Count > 0)
            ApplyState(States[0]);
    }

    private void ApplyState(BalloonToggleState state)
    {
        SetCurrentValue(ButtonIconProperty, state.ButtonIcon);
        SetCurrentValue(ButtonTextProperty, state.ButtonText);
        SetCurrentValue(DefaultBackgroundProperty, state.DefaultBackground);
        SetCurrentValue(HoveredBackgroundProperty, state.HoverBackground);
        RefreshPresentation();
    }

    private void OnStatesChanged(object? sender, EventArgs e)
    {
        var orderChanged = !_stateOrder.SequenceEqual(States);
        _stateOrder = [.. States];

        if (IsCycling && orderChanged)
        {
            ResetCycle();
            return;
        }

        if (_activeState is not null)
            ApplyState(_activeState);
        else if (States.Count > 0)
            ApplyState(States[0]);
    }

    private void OnLongPressTimerTick(object? sender, EventArgs e)
    {
        _longPressTimer.Stop();
        TryOpenStatePopup();
    }

    private void OnPopupButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not BalloonButton { CommandParameter: BalloonToggleState state })
            return;

        SelectState(state);
        e.Handled = true;
    }

    private void OnPopupPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _statePopup is null)
            return;

        _statePopup.IsOpen = false;
        e.Handled = true;
    }

    private void OnStatePopupClosed(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() => _suppressCycling = false);
    }

    private void DetachPopup()
    {
        if (_statePopup is not null)
            _statePopup.Closed -= OnStatePopupClosed;

        if (_statePopupChild is not null)
        {
            _statePopupChild.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnPopupButtonClick));
            _statePopupChild.PreviewKeyDown -= OnPopupPreviewKeyDown;
        }

        _statePopup = null;
        _statePopupChild = null;
    }
}
