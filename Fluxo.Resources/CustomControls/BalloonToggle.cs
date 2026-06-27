using System.Windows;

namespace Fluxo.Resources.CustomControls;

public class BalloonToggle : BalloonControl
{
    private static readonly DependencyPropertyKey IsCyclingPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsCycling), typeof(bool), typeof(BalloonToggle),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsCyclingProperty = IsCyclingPropertyKey.DependencyProperty;

    public static readonly DependencyProperty StatesProperty =
        DependencyProperty.Register(nameof(States), typeof(FreezableCollection<BalloonToggleState>),
            typeof(BalloonToggle));

    private BalloonToggleState? _activeState;
    private BalloonToggleState[] _stateOrder = [];

    static BalloonToggle()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(BalloonToggle),
            new FrameworkPropertyMetadata(typeof(BalloonToggle)));
    }

    public BalloonToggle()
    {
        SetValue(StatesProperty, new FreezableCollection<BalloonToggleState>());
        States.Changed += OnStatesChanged;
    }

    public bool IsCycling => (bool)GetValue(IsCyclingProperty);

    public FreezableCollection<BalloonToggleState> States =>
        (FreezableCollection<BalloonToggleState>)GetValue(StatesProperty);

    protected override void OnClick()
    {
        Cycle();
        base.OnClick();
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
}
