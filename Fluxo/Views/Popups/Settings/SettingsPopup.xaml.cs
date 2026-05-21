using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
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
    private static readonly Duration TabFadeDuration = new(TimeSpan.FromMilliseconds(150));

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
            await CrossfadeSettingsTabAsync(targetTab);
        }
        finally
        {
            _isSelectingTab = false;
        }
    }

    private async Task CrossfadeSettingsTabAsync(RadioButton targetTab)
    {
        var currentTab = GetCheckedTabButton();
        var currentContent = currentTab is null ? null : GetContentForTab(currentTab);
        var targetContent = GetContentForTab(targetTab);

        if (targetContent is null || ReferenceEquals(currentContent, targetContent))
        {
            targetTab.IsChecked = true;
            return;
        }

        targetContent.BeginAnimation(OpacityProperty, null);
        targetContent.Visibility = Visibility.Visible;
        targetContent.Opacity = 0;

        targetTab.IsChecked = true;

        if (currentContent is null)
        {
            await FadeElementAsync(targetContent, 0, 1);
            return;
        }

        currentContent.BeginAnimation(OpacityProperty, null);
        currentContent.Opacity = 1;

        await Task.WhenAll(
            FadeElementAsync(currentContent, 1, 0),
            FadeElementAsync(targetContent, 0, 1));

        currentContent.Visibility = Visibility.Collapsed;
        currentContent.Opacity = 0;
        targetContent.Opacity = 1;
    }

    private RadioButton? GetCheckedTabButton()
    {
        if (SourcesTabButton.IsChecked.GetValueOrDefault())
            return SourcesTabButton;

        if (BudgetTabButton.IsChecked.GetValueOrDefault())
            return BudgetTabButton;

        if (FixedExpensesTabButton.IsChecked.GetValueOrDefault())
            return FixedExpensesTabButton;

        if (GoalsTabButton.IsChecked.GetValueOrDefault())
            return GoalsTabButton;

        if (TagsTabButton.IsChecked.GetValueOrDefault())
            return TagsTabButton;

        if (PersonalizationTabButton.IsChecked.GetValueOrDefault())
            return PersonalizationTabButton;

        if (AboutTabButton.IsChecked.GetValueOrDefault())
            return AboutTabButton;

        return null;
    }

    private FrameworkElement? GetContentForTab(RadioButton tabButton)
    {
        if (ReferenceEquals(tabButton, SourcesTabButton))
            return SourcesTabContent;

        if (ReferenceEquals(tabButton, BudgetTabButton))
            return BudgetTabContent;

        if (ReferenceEquals(tabButton, FixedExpensesTabButton))
            return FixedExpensesTabContent;

        if (ReferenceEquals(tabButton, GoalsTabButton))
            return GoalsTabContent;

        if (ReferenceEquals(tabButton, TagsTabButton))
            return TagsTabContent;

        if (ReferenceEquals(tabButton, PersonalizationTabButton))
            return PersonalizationTabContent;

        if (ReferenceEquals(tabButton, AboutTabButton))
            return AboutTabContent;

        return null;
    }

    private static Task FadeElementAsync(UIElement element, double from, double to)
    {
        var tcs = new TaskCompletionSource<bool>();

        var animation = new DoubleAnimation(from, to, TabFadeDuration)
        {
            EasingFunction = new CubicEase { EasingMode = to < from ? EasingMode.EaseIn : EasingMode.EaseOut }
        };

        animation.Completed += (_, _) => tcs.TrySetResult(true);
        element.BeginAnimation(OpacityProperty, animation);

        return tcs.Task;
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

    internal Task<T> ShowToastWhileAsync<T>(string message, Func<global::Fluxo.Views.Popups.ToastPopup, Task<T>> work)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Toast message cannot be empty.", nameof(message));

        ArgumentNullException.ThrowIfNull(work);

        T? result = default;
        global::Fluxo.Views.Popups.ToastPopup? popup = null;
        popup = new global::Fluxo.Views.Popups.ToastPopup(message, async () =>
        {
            result = await work(popup!).ConfigureAwait(true);
        })
        {
            Owner = this
        };

        popup.ShowDialog();

        if (popup.ExecutionException is not null)
            ExceptionDispatchInfo.Capture(popup.ExecutionException).Throw();

        return Task.FromResult(result!);
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
