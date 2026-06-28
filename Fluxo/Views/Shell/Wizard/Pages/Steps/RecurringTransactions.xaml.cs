using System.Windows;
using System.Windows.Controls;
using Fluxo.Views.Shell.Wizard;

namespace Fluxo.Views.Shell.Wizard.Pages.Steps;

public partial class RecurringTransactions : UserControl
{
    public RecurringTransactions()
    {
        InitializeComponent();
    }

    private static QuickSetupWizard? FindPopup(DependencyObject source) => Window.GetWindow(source) as QuickSetupWizard;

    private void OnEditRecurringTransactionClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnEditRecurringTransactionClick(sender, e);
    }

    private void OnDeleteRecurringTransactionClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnDeleteRecurringTransactionClick(sender, e);
    }
}
