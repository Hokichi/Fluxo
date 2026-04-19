using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.Views.Shell.Wizard;

namespace Fluxo.Views.Shell.Wizard.Pages.Steps;

public partial class BudgetAllocation : UserControl
{
    public BudgetAllocation()
    {
        InitializeComponent();
    }

    private static StartupWizardPopup? FindPopup(DependencyObject source) => Window.GetWindow(source) as StartupWizardPopup;

    private void OnAllocationAdjustButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnAllocationAdjustButtonClick(sender, e);
    }

    private void OnAllocationAdjustButtonMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnAllocationAdjustButtonMouseDown(sender, e);
    }

    private void OnAllocationAdjustButtonMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnAllocationAdjustButtonMouseUp(sender, e);
    }

    private void OnAllocationAdjustButtonMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnAllocationAdjustButtonMouseLeave(sender, e);
    }
}
