using System.ComponentModel;
using System.Windows;
using Fluxo.ViewModels.Shell;

namespace Fluxo.Views.Popups;

public partial class StartupLoaderPopup : Window
{
    private bool _allowClose;

    public StartupLoaderPopup()
    {
        InitializeComponent();

        Closing += OnClosing;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    public void CloseLoader()
    {
        _allowClose = true;
        Close();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Owner is not null)
            Owner.IsEnabled = false;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (Owner is not null)
            Owner.IsEnabled = true;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
            e.Cancel = true;
    }
}