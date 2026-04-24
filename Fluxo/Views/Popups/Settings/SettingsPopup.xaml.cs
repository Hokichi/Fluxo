using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Resources.Messages;
using Fluxo.Services.Dialogs;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.Views.CustomControls;

namespace Fluxo.Views.Popups.Settings;

public partial class SettingsPopup : BasePopup, IRecipient<SettingsDialogRequestedMessage>,
    IRecipient<SettingsPopupCloseRequestedMessage>
{
    private readonly IDialogService _dialogService;
    private readonly IMessenger _messenger;
    private readonly SettingsVM _viewModel;
    private bool _allowClose;
    private bool _isHandlingCloseRequest;

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

    protected override async void OnSaveButtonClick()
    {
        var result = await _viewModel.ApplyConfigurationAsync();
        if (!result.IsSuccess)
            ShowMessage(result.ErrorMessage, "Settings");
    }

    protected override void OnRevertButtonClick()
    {
        _viewModel.RevertConfigurationChanges();
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.LoadAsync();
        }
        catch (Exception exception)
        {
            ShowMessage($"Unable to load settings.\n\n{exception.Message}", "Settings");
            _allowClose = true;
            Close();
        }
    }

    private async void OnPopupClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || _isHandlingCloseRequest || !_viewModel.HasPendingConfigurationChanges)
            return;

        e.Cancel = true;
        _isHandlingCloseRequest = true;

        try
        {
            if (FluxoMessageBox.Show(this, "Apply pending settings before closing?", "Settings",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var result = await _viewModel.ApplyConfigurationAsync();
                if (!result.IsSuccess)
                {
                    ShowMessage(result.ErrorMessage, "Settings");
                    return;
                }
            }

            _allowClose = true;
        }
        finally
        {
            _isHandlingCloseRequest = false;
        }
    }

    public void Receive(SettingsDialogRequestedMessage message)
    {
        var request = message.Value;
        switch (request.RequestType)
        {
            case SettingsDialogRequestType.AddSpendingSource when request.Payload is AddSpendingSourceVM addSpendingSource:
                _dialogService.ShowAddSpendingSource(addSpendingSource, this);
                break;

            case SettingsDialogRequestType.AddFixedExpense when request.Payload is AddFixedExpenseVM addFixedExpense:
                _dialogService.ShowAddFixedExpense(addFixedExpense, this);
                break;

            case SettingsDialogRequestType.AddSavingGoal when request.Payload is AddSavingGoalVM addSavingGoal:
                _dialogService.ShowAddSavingGoal(addSavingGoal, this);
                break;

            case SettingsDialogRequestType.SpendingSourceDetail
                when request.Payload is SpendingSourceDetailVM spendingSourceDetail:
                _dialogService.ShowSpendingSourceDetail(spendingSourceDetail, this);
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

    internal Task ShowToastWhileAsync(string message, Func<Task> work)
    {
        return _dialogService.ShowToastWhileAsync(message, work, this);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        _messenger.UnregisterAll(this);
    }

    private void ShowMessage(string? message, string title)
    {
        if (!string.IsNullOrWhiteSpace(message))
            FluxoMessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
