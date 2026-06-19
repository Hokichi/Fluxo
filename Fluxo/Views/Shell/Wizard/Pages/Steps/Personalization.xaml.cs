using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Fluxo.ViewModels.Shell.QuickSetupWizard;

namespace Fluxo.Views.Shell.Wizard.Pages.Steps;

public partial class Personalization : UserControl
{
    private bool _isSyncingPassword;
    private QuickSetupWizardPersonalizationVM? _trackedViewModel;

    public Personalization()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TrackViewModel(DataContext as QuickSetupWizardPersonalizationVM);
        SyncPasswordFieldsFromViewModel();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        TrackViewModel(null);
    }

    private void OnUiLockingPasswordBoxPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSyncingPassword || DataContext is not QuickSetupWizardPersonalizationVM viewModel)
            return;

        _isSyncingPassword = true;
        viewModel.UiLockingPassword = UiLockingPasswordBox.Password;
        UiLockingPasswordVisibleTextBox.Text = UiLockingPasswordBox.Password;
        _isSyncingPassword = false;
    }

    private void OnUiLockingPasswordVisibleTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncingPassword || DataContext is not QuickSetupWizardPersonalizationVM viewModel)
            return;

        _isSyncingPassword = true;
        viewModel.UiLockingPassword = UiLockingPasswordVisibleTextBox.Text;
        UiLockingPasswordBox.Password = UiLockingPasswordVisibleTextBox.Text;
        _isSyncingPassword = false;
    }

    private void TrackViewModel(QuickSetupWizardPersonalizationVM? viewModel)
    {
        if (ReferenceEquals(_trackedViewModel, viewModel))
            return;

        if (_trackedViewModel is not null)
            _trackedViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _trackedViewModel = viewModel;

        if (_trackedViewModel is not null)
            _trackedViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(QuickSetupWizardPersonalizationVM.UiLockingPassword), StringComparison.Ordinal))
            SyncPasswordFieldsFromViewModel();
    }

    private void SyncPasswordFieldsFromViewModel()
    {
        if (DataContext is not QuickSetupWizardPersonalizationVM viewModel)
            return;

        var password = viewModel.UiLockingPassword ?? string.Empty;
        if (string.Equals(UiLockingPasswordBox.Password, password, StringComparison.Ordinal) &&
            string.Equals(UiLockingPasswordVisibleTextBox.Text, password, StringComparison.Ordinal))
            return;

        _isSyncingPassword = true;
        UiLockingPasswordBox.Password = password;
        UiLockingPasswordVisibleTextBox.Text = password;
        _isSyncingPassword = false;
    }
}
