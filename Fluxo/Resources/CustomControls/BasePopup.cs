using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Fluxo.Resources.CustomControls;

[TemplatePart(Name = "PART_ContentRoot",  Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_PopupOverlay", Type = typeof(UIElement))]
public class BasePopup : Window, IPopupHost
{
    private const int OverlayAnimDuration = 200; // ms
    private const int PopupAnimDuration   = 180; // ms

    // --- PopupTitle ---
    public static readonly DependencyProperty PopupTitleProperty =
        DependencyProperty.Register(nameof(PopupTitle), typeof(string), typeof(BasePopup),
            new PropertyMetadata(string.Empty));

    // --- Button visibility ---
    public static readonly DependencyProperty ShowSaveButtonProperty =
        DependencyProperty.Register(nameof(ShowSaveButton), typeof(bool), typeof(BasePopup),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShowSaveAndCreateNewButtonProperty =
        DependencyProperty.Register(nameof(ShowSaveAndCreateNewButton), typeof(bool), typeof(BasePopup),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShowApplyButtonProperty =
        DependencyProperty.Register(nameof(ShowApplyButton), typeof(bool), typeof(BasePopup),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShowRevertButtonProperty =
        DependencyProperty.Register(nameof(ShowRevertButton), typeof(bool), typeof(BasePopup),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShowEditButtonProperty =
        DependencyProperty.Register(nameof(ShowEditButton), typeof(bool), typeof(BasePopup),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShowDeleteButtonProperty =
        DependencyProperty.Register(nameof(ShowDeleteButton), typeof(bool), typeof(BasePopup),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShowCloneButtonProperty =
        DependencyProperty.Register(nameof(ShowCloneButton), typeof(bool), typeof(BasePopup),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShowCancelButtonProperty =
        DependencyProperty.Register(nameof(ShowCancelButton), typeof(bool), typeof(BasePopup),
            new PropertyMetadata(false));

    private IPopupHost? _popupHost;
    private FrameworkElement? _contentRoot;
    private UIElement? _popupOverlay;
    private bool _isAnimatingClose;

    static BasePopup()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(BasePopup),
            new FrameworkPropertyMetadata(typeof(BasePopup)));
    }

    public BasePopup()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Opacity = 0;

        // Implicit styles in App.Resources don't auto-apply to derived types,
        // so bind the Style explicitly to the BasePopup resource key.
        SetResourceReference(StyleProperty, typeof(BasePopup));

        Loaded   += OnLoaded;
        Closing  += OnClosing;
        Closed   += OnClosed;
    }

    public string PopupTitle
    {
        get => (string)GetValue(PopupTitleProperty);
        set => SetValue(PopupTitleProperty, value);
    }

    public bool ShowSaveButton
    {
        get => (bool)GetValue(ShowSaveButtonProperty);
        set => SetValue(ShowSaveButtonProperty, value);
    }

    public bool ShowSaveAndCreateNewButton
    {
        get => (bool)GetValue(ShowSaveAndCreateNewButtonProperty);
        set => SetValue(ShowSaveAndCreateNewButtonProperty, value);
    }

    public bool ShowApplyButton
    {
        get => (bool)GetValue(ShowApplyButtonProperty);
        set => SetValue(ShowApplyButtonProperty, value);
    }

    public bool ShowRevertButton
    {
        get => (bool)GetValue(ShowRevertButtonProperty);
        set => SetValue(ShowRevertButtonProperty, value);
    }

    public bool ShowEditButton
    {
        get => (bool)GetValue(ShowEditButtonProperty);
        set => SetValue(ShowEditButtonProperty, value);
    }

    public bool ShowDeleteButton
    {
        get => (bool)GetValue(ShowDeleteButtonProperty);
        set => SetValue(ShowDeleteButtonProperty, value);
    }


    public bool ShowCloneButton
    {
        get => (bool)GetValue(ShowCloneButtonProperty);
        set => SetValue(ShowCloneButtonProperty, value);
    }


    public bool ShowCancelButton
    {
        get => (bool)GetValue(ShowCancelButtonProperty);
        set => SetValue(ShowCancelButtonProperty, value);
    }

    // ── Template wiring ─────────────────────────────────────────────

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        WireButton("PART_CloseButton", _ => OnCloseButtonClick());
        WireButton("PART_SaveButton", _ => OnSaveButtonClick());
        WireButton("PART_SaveAndCreateNewButton", _ => OnSaveAndCreateNewButtonClick());
        WireButton("PART_ApplyButton", _ => OnApplyButtonClick());
        WireButton("PART_RevertButton", _ => OnRevertButtonClick());
        WireButton("PART_EditButton", _ => OnEditButtonClick());
        WireButton("PART_DeleteButton", _ => OnDeleteButtonClick());
        WireButton("PART_CloneButton", _ => OnCloneButtonClick());
        WireButton("PART_CancelButton", _ => OnCancelButtonClick());

        _contentRoot  = GetTemplateChild("PART_ContentRoot")  as FrameworkElement;
        _popupOverlay = GetTemplateChild("PART_PopupOverlay") as UIElement;
    }

    private void WireButton(string partName, Action<RoutedEventArgs> handler)
    {
        if (GetTemplateChild(partName) is BalloonButton btn)
            btn.Click += (_, e) => handler(e);
    }

    // ── Virtual button handlers (override in child popups) ──────────

    protected virtual void OnCloseButtonClick()
    {
        Close();
    }

    protected virtual void OnSaveButtonClick()
    {
    }

    protected virtual void OnSaveAndCreateNewButtonClick()
    {
    }

    protected virtual void OnApplyButtonClick()
    {
    }

    protected virtual void OnRevertButtonClick()
    {
    }

    protected virtual void OnEditButtonClick()
    {
    }

    protected virtual void OnDeleteButtonClick()
    {
    }

    protected virtual void OnCloneButtonClick()
    {
    }

    protected virtual void OnCancelButtonClick()
    {
    }

    // ── Keyboard shortcuts ──────────────────────────────────────────

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        switch (e.Key)
        {
            case Key.Escape:
                OnCloseButtonClick();
                e.Handled = true;
                break;

            case Key.Enter when Keyboard.Modifiers == ModifierKeys.Shift:
                if (ShowSaveAndCreateNewButton)
                {
                    OnSaveAndCreateNewButtonClick();
                    e.Handled = true;
                }

                break;

            case Key.Enter when Keyboard.Modifiers == ModifierKeys.None:
                if (ShowApplyButton)
                {
                    OnApplyButtonClick();
                    e.Handled = true;
                }
                else if (ShowSaveButton)
                {
                    OnSaveButtonClick();
                    e.Handled = true;
                }

                break;
            case Key.Delete:
                OnDeleteButtonClick();
                e.Handled = true;
                break;
        }
    }

    // ── Overlay & blur on owner ─────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _popupHost = Owner as IPopupHost;
        _popupHost?.ShowPopupOverlay();

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(PopupAnimDuration))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fadeIn);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isAnimatingClose) return;

        e.Cancel = true;
        _isAnimatingClose = true;

        _popupHost?.HidePopupOverlay();

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(PopupAnimDuration))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        fadeOut.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (!_isAnimatingClose)
            _popupHost?.HidePopupOverlay();
    }

    public void ShowPopupOverlay()
    {
        if (_contentRoot is null || _popupOverlay is null) return;

        _contentRoot.Effect = new BlurEffect { Radius = 20, RenderingBias = RenderingBias.Performance };

        _popupOverlay.Visibility = Visibility.Visible;
        var fadeIn = new DoubleAnimation(0, 0.5, TimeSpan.FromMilliseconds(OverlayAnimDuration))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        _popupOverlay.BeginAnimation(OpacityProperty, fadeIn);
    }

    public void HidePopupOverlay()
    {
        if (_contentRoot is null || _popupOverlay is null) return;

        _contentRoot.Effect = null;

        var fadeOut = new DoubleAnimation(0.5, 0, TimeSpan.FromMilliseconds(OverlayAnimDuration))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) =>
        {
            _popupOverlay.BeginAnimation(OpacityProperty, null);
            _popupOverlay.Opacity = 0;
            _popupOverlay.Visibility = Visibility.Collapsed;
        };
        _popupOverlay.BeginAnimation(OpacityProperty, fadeOut);
    }
}
