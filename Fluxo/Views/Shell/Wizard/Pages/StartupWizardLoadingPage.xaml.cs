using System.Windows;
using System.Windows.Controls;

namespace Fluxo.Views.Shell.Wizard.Pages;

public partial class QuickSetupWizardLoadingPage : UserControl
{
    public QuickSetupWizardLoadingPage()
    {
        InitializeComponent();
    }

    public UIElement ContentElement => PageContent;
}
