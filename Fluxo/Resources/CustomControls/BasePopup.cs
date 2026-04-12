using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Fluxo.Views.Shell;

namespace Fluxo.Resources.CustomControls;

public class BasePopup : Window
{
    private const int OverlayAnimDuration = 200; // ms

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

    private MainWindow? _ownerWindow;

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

        // Implicit styles in App.Resources don't auto-apply to derived types,
        // so bind the Style explicitly to the BasePopup resource key.
        SetResourceReference(StyleProperty, typeof(BasePopup));

        Loaded += OnLoaded;
        Closed += OnClosed;
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

    // ── Overlay & blur on MainWindow ────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ownerWindow = Owner as MainWindow;
        _ownerWindow?.ShowPopupOverlay();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _ownerWindow?.HidePopupOverlay();
    }
}