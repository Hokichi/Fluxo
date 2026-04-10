using System.Windows;
using System.Windows.Media;
using Fluxo.Resources.CustomControls;

namespace Fluxo.Views.Popups;

public partial class MessageBoxPopup : BasePopup
{
    private MessageBoxButton _buttons;

    public MessageBoxPopup(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None)
    {
        InitializeComponent();

        PopupTitle = string.IsNullOrWhiteSpace(title) ? "Message" : title;
        MessageTextBlock.Text = message;

        ConfigureButtons(buttons);
        ConfigureIcon(icon);
    }

    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    private void ConfigureButtons(MessageBoxButton buttons)
    {
        _buttons = buttons;

        switch (buttons)
        {
            case MessageBoxButton.OK:
                ConfigureButton(PrimaryButton, "OK", MessageBoxResult.OK, isDefault: true);
                PrimaryButton.IsCancel = true;
                break;

            case MessageBoxButton.OKCancel:
                ConfigureButton(PrimaryButton, "OK", MessageBoxResult.OK, isDefault: true);
                ConfigureButton(SecondaryButton, "Cancel", MessageBoxResult.Cancel, isCancel: true);
                break;

            case MessageBoxButton.YesNo:
                ConfigureButton(PrimaryButton, "Yes", MessageBoxResult.Yes, isDefault: true);
                ConfigureButton(SecondaryButton, "No", MessageBoxResult.No, isCancel: true);
                break;

            case MessageBoxButton.YesNoCancel:
                ConfigureButton(PrimaryButton, "Yes", MessageBoxResult.Yes, isDefault: true);
                ConfigureButton(SecondaryButton, "No", MessageBoxResult.No);
                ConfigureButton(TertiaryButton, "Cancel", MessageBoxResult.Cancel, isCancel: true);
                break;

            default:
                ConfigureButton(PrimaryButton, "OK", MessageBoxResult.OK, isDefault: true);
                PrimaryButton.IsCancel = true;
                break;
        }
    }

    private void ConfigureButton(System.Windows.Controls.Button button, string content, MessageBoxResult result,
        bool isDefault = false, bool isCancel = false)
    {
        button.Content = content;
        button.Tag = result;
        button.Visibility = Visibility.Visible;
        button.IsDefault = isDefault;
        button.IsCancel = isCancel;
    }

    private void ConfigureIcon(MessageBoxImage icon)
    {
        var (geometryKey, accentBrushKey) = icon switch
        {
            MessageBoxImage.Error or MessageBoxImage.Stop or MessageBoxImage.Hand => ("Ban", "Brush.Danger"),
            MessageBoxImage.Warning or MessageBoxImage.Exclamation => ("Info", "Brush.Warning"),
            MessageBoxImage.Question => ("Info", "Brush.Primary.Hover"),
            MessageBoxImage.Information or MessageBoxImage.Asterisk => ("Info", "Brush.Info"),
            _ => ("Info", "Brush.Text.Secondary")
        };

        var accentBrush = TryFindResource(accentBrushKey) as Brush ?? Brushes.White;
        MessageIcon.Data = TryFindResource(geometryKey) as Geometry;
        MessageIcon.Fill = accentBrush;
        IconBadge.Background = CreateBadgeBackground(accentBrush);
    }

    protected override void OnCloseButtonClick()
    {
        Result = GetDismissResult();
        Close();
    }

    private void OnPrimaryButtonClick(object sender, RoutedEventArgs e)
    {
        SetResultFrom(sender);
    }

    private void OnSecondaryButtonClick(object sender, RoutedEventArgs e)
    {
        SetResultFrom(sender);
    }

    private void OnTertiaryButtonClick(object sender, RoutedEventArgs e)
    {
        SetResultFrom(sender);
    }

    private void SetResultFrom(object sender)
    {
        if (sender is System.Windows.Controls.Button { Tag: MessageBoxResult result })
            Result = result;

        Close();
    }

    private MessageBoxResult GetDismissResult()
    {
        return _buttons switch
        {
            MessageBoxButton.OK => MessageBoxResult.OK,
            MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
            MessageBoxButton.YesNo => MessageBoxResult.No,
            MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
            _ => MessageBoxResult.None
        };
    }

    private static Brush CreateBadgeBackground(Brush accentBrush)
    {
        if (accentBrush is not SolidColorBrush solidColorBrush)
            return accentBrush;

        return new SolidColorBrush(solidColorBrush.Color)
        {
            Opacity = 0.16
        };
    }
}
