using System.Windows;
using System.Windows.Input;

namespace Fluxo.Views.Popups;

public partial class AppUnlockPopup : BasePopup
{
    private readonly Func<string, bool> _tryUnlock;

    public AppUnlockPopup(Func<string, bool> tryUnlock)
    {
        ArgumentNullException.ThrowIfNull(tryUnlock);

        InitializeComponent();
        _tryUnlock = tryUnlock;
        Loaded += (_, _) => UnlockPasswordBox.Focus();
    }

    protected override void OnCloseButtonClick()
    {
        DialogResult = false;
        Close();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TryUnlock();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private void OnUnlockButtonClick(object sender, RoutedEventArgs e)
    {
        TryUnlock();
    }

    private void OnUnlockPasswordBoxPasswordChanged(object sender, RoutedEventArgs e)
    {
        HideValidation();
    }

    private void TryUnlock()
    {
        if (_tryUnlock(UnlockPasswordBox.Password))
        {
            DialogResult = true;
            Close();
            return;
        }

        ValidationText.Visibility = Visibility.Visible;
        UnlockPasswordBox.SelectAll();
        UnlockPasswordBox.Focus();
    }

    private void HideValidation()
    {
        ValidationText.Visibility = Visibility.Collapsed;
    }
}
