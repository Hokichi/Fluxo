using System.Windows.Controls;
using System.Windows;
using Fluxo.Views.Shell.Wizard;

namespace Fluxo.Views.Shell.Wizard.Pages;

public partial class StartupWizardFinalPage : UserControl
{
    public StartupWizardFinalPage()
    {
        InitializeComponent();
    }

    public UIElement ContentElement => PageContent;

    private static StartupWizardPopup? FindPopup(DependencyObject source) => Window.GetWindow(source) as StartupWizardPopup;

    private void OnFinishClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnFinishClick(sender, e);
    }
}
