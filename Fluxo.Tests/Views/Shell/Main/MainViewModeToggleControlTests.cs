using System.Threading;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Resources.Resources.Messages;
using Fluxo.ViewModels.Shell.Main;
using Fluxo.Views.Shell.Main.Controls;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class MainViewModeToggleControlTests
{
    [Fact]
    public void MoveToCurrentButton_WhenPeriodIsCurrent_IsCollapsed()
    {
        RunOnStaThread(() =>
        {
            EnsureApplicationResources();
            var messenger = new WeakReferenceMessenger();
            var viewModel = new MainViewModeToggleVM(messenger);
            messenger.Send(new SpinnerPeriodStateChangedMessage(new SpinnerPeriodState(
                IsAtCurrentPeriod: true,
                IsSpinnerVisible: true,
                MoveToCurrentLabel: "Move to today")));

            var control = new MainViewModeToggleControl { DataContext = viewModel };
            var window = new Window { Content = control };
            try
            {
                window.Show();

                var button = ((StackPanel)control.Content).Children.OfType<Button>().Single();
                Assert.True(viewModel.IsAtCurrentPeriod);
                button.GetBindingExpression(UIElement.VisibilityProperty)!.UpdateTarget();

                Assert.Equal(Visibility.Collapsed, button.Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }

    private static void EnsureApplicationResources()
    {
        var application = Application.Current ?? new Application();
        foreach (var resource in new[]
                 {
                     "Theme.xaml", "Fonts.xaml", "Icons.xaml", "Converters.xaml",
                     "Styles/ContainerStyles.xaml", "Styles/ButtonStyles.xaml", "Styles/TextBoxStyles.xaml",
                     "Styles/GlobalStyles.xaml", "Styles/PopupStyles.xaml", "Styles/MainWindowStyles.xaml",
                     "Styles/SettingsStyle.xaml", "Styles/StepNavigatorStyle.xaml", "Styles/QuickSetupWizardStyle.xaml"
                 })
        {
            if (application.Resources.MergedDictionaries.Any(dictionary =>
                    dictionary.Source?.OriginalString.EndsWith($"Resources/{resource}", StringComparison.OrdinalIgnoreCase) == true))
                continue;

            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"/Fluxo.Resources;component/Resources/{resource}", UriKind.Relative)
            });
        }
    }
}
