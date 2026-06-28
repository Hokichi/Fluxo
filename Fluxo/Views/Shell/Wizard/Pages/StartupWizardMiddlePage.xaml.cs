using System.Windows;
using System.Windows.Controls;
using Fluxo.Views.Shell.Wizard;

namespace Fluxo.Views.Shell.Wizard.Pages;

public partial class QuickSetupWizardMiddlePage : UserControl
{
    public QuickSetupWizardMiddlePage()
    {
        InitializeComponent();
    }

    public UIElement ContentColumnElement => ContentColumn;
    public UIElement StepContentElement => StepContent;

    public void ScrollCurrentStepToTop()
    {
        MiddleStepScrollViewer.ScrollToVerticalOffset(0);
    }

    public Border? GetStripeForStep(int stepIndex) => stepIndex switch
    {
        2 => Step1Stripe,
        3 => Step2Stripe,
        4 => Step3Stripe,
        5 => Step4Stripe,
        6 => Step5Stripe,
        7 => Step6Stripe,
        8 => Step7Stripe,
        _ => null
    };

    private static QuickSetupWizard? FindPopup(DependencyObject source) => Window.GetWindow(source) as QuickSetupWizard;

    private void OnAddAccountClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnAddAccountClick(sender, e);
    }

    private void OnAddRecurringTransactionClick(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
            FindPopup(source)?.OnAddRecurringTransactionClick(sender, e);
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
