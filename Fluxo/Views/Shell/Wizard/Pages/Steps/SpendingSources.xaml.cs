using System.Windows;
using System.Windows.Controls;
using Fluxo.Views.Shell.Wizard;

namespace Fluxo.Views.Shell.Wizard.Pages.Steps;

public partial class SpendingSources : UserControl
{
    public SpendingSources()
    {
        InitializeComponent();
    }

    private static QuickSetupWizard? FindPopup(DependencyObject source) => Window.GetWindow(source) as QuickSetupWizard;

    private void OnEditSpendingSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnEditSpendingSourceClick(sender, e);
    }

    private void OnDeleteSpendingSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnDeleteSpendingSourceClick(sender, e);
    }
}
