using System.Windows;
using System.Windows.Controls;
using Fluxo.Views.Shell.Wizard;

namespace Fluxo.Views.Shell.Wizard.Pages.Steps;

public partial class StartupWizardStep4Page : UserControl
{
    public StartupWizardStep4Page()
    {
        InitializeComponent();
    }

    private static StartupWizardPopup? FindPopup(DependencyObject source) => Window.GetWindow(source) as StartupWizardPopup;

    private void OnEditSavingGoalClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnEditSavingGoalClick(sender, e);
    }

    private void OnDeleteSavingGoalClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnDeleteSavingGoalClick(sender, e);
    }
}
