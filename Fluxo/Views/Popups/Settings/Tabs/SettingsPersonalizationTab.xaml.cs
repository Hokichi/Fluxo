using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using Fluxo.Views.Popups.Settings;

namespace Fluxo.Views.Popups.Settings.Tabs;

public partial class SettingsPersonalizationTab : UserControl
{
    private readonly DispatcherTimer _usernameAutosaveTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private bool _isLoaded;

    public SettingsPersonalizationTab()
    {
        InitializeComponent();

        _usernameAutosaveTimer.Tick += OnUsernameAutosaveTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        _usernameAutosaveTimer.Stop();
    }

    private void OnUsernameTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isLoaded || !UsernameTextBox.IsKeyboardFocusWithin)
            return;

        _usernameAutosaveTimer.Stop();
        _usernameAutosaveTimer.Start();
    }

    private void OnUsernameTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        _usernameAutosaveTimer.Stop();
        RequestPersonalizationAutosave();
    }

    private void OnUsernameAutosaveTimerTick(object? sender, EventArgs e)
    {
        _usernameAutosaveTimer.Stop();
        RequestPersonalizationAutosave();
    }

    private void RequestPersonalizationAutosave()
    {
        if (Window.GetWindow(this) is SettingsPopup settingsPopup)
            settingsPopup.RequestPersonalizationAutosave();
    }
}
