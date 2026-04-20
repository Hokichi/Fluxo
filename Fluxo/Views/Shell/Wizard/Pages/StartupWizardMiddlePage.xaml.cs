using System.Windows;
using System.Windows.Controls;
using Fluxo.Views.Shell.Wizard;

namespace Fluxo.Views.Shell.Wizard.Pages;

public partial class StartupWizardMiddlePage : UserControl
{
    public StartupWizardMiddlePage()
    {
        InitializeComponent();
    }

    public UIElement ContentColumnElement => ContentColumn;

    public Border? GetStripeForStep(int stepIndex) => stepIndex switch
    {
        2 => Step1Stripe,
        3 => Step2Stripe,
        4 => Step3Stripe,
        5 => Step4Stripe,
        6 => Step5Stripe,
        7 => Step6Stripe,
        _ => null
    };

    private static StartupWizardPopup? FindPopup(DependencyObject source) => Window.GetWindow(source) as StartupWizardPopup;

    private void OnAddSpendingSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnAddSpendingSourceClick(sender, e);
    }

    private void OnAddFixedExpenseClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnAddFixedExpenseClick(sender, e);
    }

    private void OnAddSavingGoalClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnAddSavingGoalClick(sender, e);
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnBackClick(sender, e);
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnNextClick(sender, e);
    }
}
