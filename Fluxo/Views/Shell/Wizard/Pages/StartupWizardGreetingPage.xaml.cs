using System.Windows.Controls;
using System.Windows;
using Fluxo.Views.Shell.Wizard;

namespace Fluxo.Views.Shell.Wizard.Pages;

public partial class QuickSetupWizardGreetingPage : UserControl
{
    public QuickSetupWizardGreetingPage()
    {
        InitializeComponent();
    }

    private static QuickSetupWizard? FindPopup(DependencyObject source) => Window.GetWindow(source) as QuickSetupWizard;

    private void OnContinueClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnNextClick(sender, e);
    }
}
