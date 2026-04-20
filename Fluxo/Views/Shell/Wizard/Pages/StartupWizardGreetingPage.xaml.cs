using System.Windows.Controls;
using System.Windows;
using Fluxo.Views.Shell.Wizard;

namespace Fluxo.Views.Shell.Wizard.Pages;

public partial class StartupWizardGreetingPage : UserControl
{
    public StartupWizardGreetingPage()
    {
        InitializeComponent();
    }

    private static StartupWizardPopup? FindPopup(DependencyObject source) => Window.GetWindow(source) as StartupWizardPopup;

    private void OnContinueClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnNextClick(sender, e);
    }
}
