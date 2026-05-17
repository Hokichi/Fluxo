using System.Windows.Controls;
using System.Windows;
using Fluxo.Views.Shell.Wizard;

namespace Fluxo.Views.Shell.Wizard.Pages;

public partial class QuickSetupWizardFinalPage : UserControl
{
    public QuickSetupWizardFinalPage()
    {
        InitializeComponent();
    }

    public UIElement ContentElement => PageContent;

    private static QuickSetupWizard? FindPopup(DependencyObject source) => Window.GetWindow(source) as QuickSetupWizard;

    private void OnFinishClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnFinishClick(sender, e);
    }
}
