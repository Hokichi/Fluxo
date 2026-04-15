using System.Windows;
using System.Windows.Controls;
using Fluxo.Views.Shell.Wizard;

namespace Fluxo.Views.Shell.Wizard.Pages.Steps;

public partial class StartupWizardStep3Page : UserControl
{
    public StartupWizardStep3Page()
    {
        InitializeComponent();
    }

    private static StartupWizardPopup? FindPopup(DependencyObject source) => Window.GetWindow(source) as StartupWizardPopup;

    private void OnEditFixedExpenseClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnEditFixedExpenseClick(sender, e);
    }

    private void OnDeleteFixedExpenseClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnDeleteFixedExpenseClick(sender, e);
    }
}
