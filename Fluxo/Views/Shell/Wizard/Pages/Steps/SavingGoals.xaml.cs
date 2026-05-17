using System.Windows;
using System.Windows.Controls;
using Fluxo.Views.Shell.Wizard;

namespace Fluxo.Views.Shell.Wizard.Pages.Steps;

public partial class SavingGoals : UserControl
{
    public SavingGoals()
    {
        InitializeComponent();
    }

    private static QuickSetupWizard? FindPopup(DependencyObject source) => Window.GetWindow(source) as QuickSetupWizard;

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
