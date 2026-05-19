using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.Dialogs;
using Fluxo.Services.Logging;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.Views.Popups.Settings;

public partial class SettingsPopup : BasePopup, IRecipient<SettingsDialogRequestedMessage>,
    IRecipient<SettingsPopupCloseRequestedMessage>, IRecipient<SettingsPendingChangesChangedMessage>
{
    private readonly IDialogService _dialogService;
    private readonly IMessenger _messenger;
    private readonly SettingsVM _viewModel;
    private bool _allowClose;
    private bool _isLoaded;
    private bool _isHandlingCloseRequest;
    private bool _isSavingConfiguration;
    private bool _isSelectingTab;

    public SettingsPopup(SettingsVM viewModel, IDialogService dialogService, IMessenger messenger)
    {
        InitializeComponent();

        _dialogService = dialogService;
        _messenger = messenger;
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoadedAsync;
        Closing += OnPopupClosing;
        Closed += OnPopupClosed;

        _messenger.RegisterAll(this);
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.LoadAsync();
            _isLoaded = true;
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to load settings popup.");
            ShowMessage(FluxoLogManager.CreateFailureMessage("load settings"), "Settings");
            _allowClose = true;
            Close();
        }
    }

    private async void OnPopupClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || _isHandlingCloseRequest)
            return;

        e.Cancel = true;
        _isHandlingCloseRequest = true;

        try
        {
            if (!await CanLeaveCurrentSettingsTabAsync())
                return;

            if (_viewModel.HasPendingPersonalizationConfigurationChanges &&
                !(await SaveConfigurationChangesAsync()).IsSuccess)
            {
                return;
            }

            _allowClose = true;
            await Dispatcher.BeginInvoke(Close, DispatcherPriority.Background);
        }
        finally
        {
            _isHandlingCloseRequest = false;
        }
    }

    private async void OnSettingsTabPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isSelectingTab ||
            sender is not RadioButton targetTab ||
            targetTab.IsChecked.GetValueOrDefault())
            return;

        e.Handled = true;
        if (!await CanLeaveCurrentSettingsTabAsync())
            return;

        _isSelectingTab = true;
        try
        {
            targetTab.IsChecked = true;
        }
        finally
        {
            _isSelectingTab = false;
        }
    }

    private async Task<bool> CanLeaveCurrentSettingsTabAsync()
    {
        if (!BudgetTabButton.IsChecked.GetValueOrDefault() ||
            !_viewModel.HasPendingBudgetConfigurationChanges)
        {
            return true;
        }

        if (_viewModel.CanSaveBudgetConfiguration)
            return (await SaveConfigurationChangesAsync()).IsSuccess;

        var message = string.IsNullOrWhiteSpace(_viewModel.BudgetConfigurationErrorMessage)
            ? "Budget Allocation is not valid."
            : _viewModel.BudgetConfigurationErrorMessage;

        if (FluxoMessageBox.Show(this, $"{message}\n\nDo you want to adjust it?", "Budget Allocation",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            return false;
        }

        _viewModel.DiscardBudgetConfigurationChanges();
        return true;
    }

    public void Receive(SettingsDialogRequestedMessage message)
    {
        var request = message.Value;
        switch (request.RequestType)
        {
            case SettingsDialogRequestType.AddSpendingSource when request.Payload is null:
                _dialogService.ShowAddSpendingSource(_viewModel.CreateAddSpendingSourceViewModel(), this);
                break;

            case SettingsDialogRequestType.AddSpendingSource when request.Payload is AddSpendingSourceVM addSpendingSource:
                _dialogService.ShowAddSpendingSource(addSpendingSource, this);
                break;

            case SettingsDialogRequestType.AddRecurringTransaction when request.Payload is QuickAddVM quickAdd:
                _dialogService.ShowAddNewTransaction(quickAdd, this);
                break;

            case SettingsDialogRequestType.AddSavingGoal when request.Payload is AddSavingGoalVM addSavingGoal:
                _dialogService.ShowAddSavingGoal(addSavingGoal, this);
                break;

            case SettingsDialogRequestType.SpendingSourceDetail
                when request.Payload is SpendingSourceDetailVM spendingSourceDetail:
                _dialogService.ShowSpendingSourceDetail(spendingSourceDetail, this);
                break;

            case SettingsDialogRequestType.AddTag when request.Payload is SettingsTagDialogRequest tagDialogRequest:
                _dialogService.ShowAddTag(tagDialogRequest.ViewModel, tagDialogRequest.SaveTagAsync, this);
                break;

            case SettingsDialogRequestType.AddTag when request.Payload is SettingsTagsTabVM tagsTab:
                _dialogService.ShowAddTag(tagsTab, this);
                break;

            case SettingsDialogRequestType.FeaturePlaceholder:
                _dialogService.ShowFeaturePlaceholder(request.Title ?? "Settings",
                    request.Message ?? "This flow is still being built.", this);
                break;
        }
    }

    public void Receive(SettingsPopupCloseRequestedMessage message)
    {
        if (message.Value.AllowClose)
            _allowClose = true;

        Close();
    }

    public void Receive(SettingsPendingChangesChangedMessage message)
    {
        if (!_isLoaded ||
            message.Value.TabKey != SettingsTabKey.Personalization ||
            !message.Value.HasPendingChanges ||
            IsPersonalizationTextInputFocused())
        {
            return;
        }

        RequestPersonalizationAutosave();
    }

    internal void RequestPersonalizationAutosave()
    {
        if (!_isLoaded || !_viewModel.HasPendingPersonalizationConfigurationChanges)
            return;

        _ = SaveConfigurationChangesAsync();
    }

    internal Task ShowToastWhileAsync(string message, Func<Task> work)
    {
        return _dialogService.ShowToastWhileAsync(message, work, this);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        _messenger.UnregisterAll(this);
    }

    private async Task<SettingsOperationResult> SaveConfigurationChangesAsync()
    {
        if (_isSavingConfiguration)
            return SettingsOperationResult.Success();

        _isSavingConfiguration = true;
        try
        {
            var result = await _viewModel.SaveConfigurationChangesAsync();
            if (!result.IsSuccess)
                ShowMessage(result.ErrorMessage, "Settings");

            return result;
        }
        finally
        {
            _isSavingConfiguration = false;
        }
    }

    private bool IsPersonalizationTextInputFocused()
    {
        return Keyboard.FocusedElement is TextBox textBox &&
               ReferenceEquals(textBox.DataContext, _viewModel.PersonalizationTab);
    }

    private void ShowMessage(string? message, string title)
    {
        if (!string.IsNullOrWhiteSpace(message))
            FluxoMessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
