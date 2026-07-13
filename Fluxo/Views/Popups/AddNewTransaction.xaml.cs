using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using Fluxo.Resources.Infrastructure;
using Fluxo.Services.Dialogs;
using Fluxo.Services.Notifications;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.Views.Popups;

public partial class AddNewTransaction : BasePopup
{
    private enum MoreTagsPopupLifecycleState
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    private readonly IDialogService _dialogService;
    private readonly SettingsTagsTabVM _settingsTagsTabViewModel;
    private readonly AddNewTransactionVM _viewModel;
    private bool _isHandlingAddTagSelection;
    private readonly DispatcherTimer _moreTagsHoverCloseTimer;
    private MoreTagsPopupLifecycleState _moreTagsPopupState = MoreTagsPopupLifecycleState.Closed;
    private bool _isSyncingNoteDocument;
    private bool _isFinalizingProcessing;

    public AddNewTransaction(
        AddNewTransactionVM viewModel,
        IDialogService dialogService,
        SettingsTagsTabVM settingsTagsTabViewModel)
    {
        InitializeComponent();

        _dialogService = dialogService;
        _settingsTagsTabViewModel = settingsTagsTabViewModel;
        _viewModel = viewModel;
        DataContext = viewModel;
        _moreTagsHoverCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _moreTagsHoverCloseTimer.Tick += (_, _) =>
        {
            _moreTagsHoverCloseTimer.Stop();
            TryCloseMoreTagsPopupIfNotPinned();
        };

        Loaded += async (_, _) =>
        {
            await _viewModel.EnsureTagsLoadedAsync();
            if (_viewModel.IsHistoryOpen)
                await _viewModel.LoadHistoryAsync();
            RecalculateTagLayout();
            SyncMoreTagsPopupState();
            SyncNameSuggestionsPopupState();
            SyncNoteDocumentFromViewModel();
            _viewModel.BeginChangeTracking();
            FocusPrimaryInput();
        };

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Unloaded += (_, _) => _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        TagsDockPanel.SizeChanged += (_, _) => RecalculateTagLayout();
        PreviewMouseDown += OnPopupPreviewMouseDown;
    }

    protected override async void OnSaveButtonClick()
    {
        if (!await ShouldSaveCurrentTransactionAsync())
            return;

        var result = await _viewModel.SaveAsync(false);
        if (!result.IsSuccess)
        {
            ShowValidationMessage(result.ErrorMessage);
            return;
        }

        Close();
    }

    protected override async void OnSaveAndCreateNewButtonClick()
    {
        if (!await ShouldSaveCurrentTransactionAsync())
            return;

        var result = await _viewModel.SaveAsync(true);
        if (!result.IsSuccess)
        {
            ShowValidationMessage(result.ErrorMessage);
            return;
        }

        NoteRichTextBox.Text = string.Empty;
        _viewModel.BeginChangeTracking();
        FocusPrimaryInput();
    }

    private async Task<bool> ShouldSaveCurrentTransactionAsync()
    {
        if (_viewModel.TryGetRepaymentCorrection(out var correctedAmount))
        {
            var useCorrectAmount = _dialogService.ShowWarning(
                $"Repayment exceeds the credit account's spent amount. Use {correctedAmount:N2} instead?",
                "Invalid Repayment",
                this,
                MessageBoxButton.YesNo) == MessageBoxResult.Yes;
            if (!useCorrectAmount)
            {
                _viewModel.RejectRepaymentCorrection();
                return false;
            }

            _viewModel.AcceptRepaymentCorrection();
        }

        if (!await _viewModel.HasSimilarTransactionAsync())
            return true;

        return _dialogService.ShowWarning(
            "Potentially duplicated transaction found. Would you like to save the current one?",
            "Add New Transaction",
            this,
            MessageBoxButton.YesNo) == MessageBoxResult.Yes;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && NoteRichTextBox.IsKeyboardFocusWithin && Keyboard.Modifiers != ModifierKeys.Shift)
            return;

        base.OnPreviewKeyDown(e);
    }

    protected override void OnCloseButtonClick()
    {
        if (_viewModel.HasChanges)
        {
            var confirmation = FluxoMessageBox.Show(
                this,
                "Close without saving your changes?",
                "Add New Transaction",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
                return;
        }

        base.OnCloseButtonClick();
    }

    private void OnNoteTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncingNoteDocument)
            return;

        _viewModel.NoteText = NoteRichTextBox.Text;
    }

    private void ShowValidationMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        FloatingNotificationPublisher.SaveFailed("Transaction not saved", [message]);
    }

    private void FocusPrimaryInput()
    {
        if (_viewModel.IsGoal)
        {
            ExpenseAmountTextBox.Focus();
            return;
        }

        if (_viewModel.IsRepayment)
        {
            RepaymentAmountTextBox.Focus();
            return;
        }

        ExpenseNameTextBox.Focus();
    }

    protected override async void OnNextButtonClick() => await SaveAndAdvanceAsync();

    protected override async void OnFinishButtonClick() => await SaveAndAdvanceAsync();

    protected override void OnBackButtonClick()
    {
        _viewModel.NavigatePreviousProcessing();
        SyncNoteDocumentFromViewModel();
        FocusPrimaryInput();
    }

    protected override void OnSkipButtonClick()
    {
        if (!_viewModel.SkipCurrentProcessing())
            Close();
        else
        {
            SyncNoteDocumentFromViewModel();
            FocusPrimaryInput();
        }
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (!_viewModel.IsProcessingSession || _isFinalizingProcessing)
            return;

        e.Cancel = true;
        _isFinalizingProcessing = true;
        var result = await _viewModel.PersistProcessedItemsAsync();
        _isFinalizingProcessing = false;
        if (!result.IsSuccess)
        {
            ShowValidationMessage(result.ErrorMessage);
            return;
        }
    }

    private async Task SaveAndAdvanceAsync()
    {
        if (!await ShouldSaveCurrentTransactionAsync())
            return;

        var result = await _viewModel.SaveCurrentAndAdvanceAsync();
        if (!result.IsSuccess)
        {
            ShowValidationMessage(result.ErrorMessage);
            return;
        }

        if (!_viewModel.IsProcessingSession)
            Close();
    }

    private void SyncNoteDocumentFromViewModel()
    {
        _isSyncingNoteDocument = true;

        try
        {
            var noteText = _viewModel.NoteText ?? string.Empty;
            NoteRichTextBox.Text = noteText;
        }
        finally
        {
            _isSyncingNoteDocument = false;
        }
    }

    private async void OnAddTagClick(object sender, RoutedEventArgs e)
    {
        if (_isHandlingAddTagSelection)
            return;

        _isHandlingAddTagSelection = true;
        try
        {
            var previousTagNames = _viewModel.VisibleTags
                .Concat(_viewModel.OverflowTags)
                .Select(tag => tag.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _dialogService.ShowAddTag(_settingsTagsTabViewModel, this);
            await _viewModel.EnsureTagsLoadedAsync();
            RecalculateTagLayout();

            var newTag = _viewModel.VisibleTags
                .Concat(_viewModel.OverflowTags)
                .FirstOrDefault(tag =>
                    !string.IsNullOrWhiteSpace(tag.Name) &&
                    !previousTagNames.Contains(tag.Name));

            if (newTag is not null)
                _viewModel.SelectedTag = newTag;
        }
        finally
        {
            _isHandlingAddTagSelection = false;
        }
    }

    private void OnTagSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            RecalculateTagLayout();
            SyncMoreTagsPopupState();
        }));
    }

    private void OnTransactionNameSuggestionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox)
            return;

        if (listBox.SelectedItem is not AddNewTransactionVM.AddNewTransactionSuggestion suggestion)
            return;

        _viewModel.ApplyTransactionNameSuggestion(suggestion);
        SyncNoteDocumentFromViewModel();
        listBox.SelectedItem = null;
        SyncNameSuggestionsPopupState();
    }

    private void OnHistoryListPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject source)
            return;

        var scrollViewer = DependencyObjectTree.FindAncestor<ScrollViewer>(DependencyObjectTree.GetParent(source));
        if (scrollViewer is null)
            return;

        var wheelSteps = e.Delta / (double)Mouse.MouseWheelDeltaForOneLine;
        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - wheelSteps * 48d);
        e.Handled = true;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AddNewTransactionVM.HasTransactionNameSuggestions) or
            nameof(AddNewTransactionVM.IsExpense) or
            nameof(AddNewTransactionVM.IsGoal) or
            nameof(AddNewTransactionVM.IsIncome))
            SyncNameSuggestionsPopupState();

        if (e.PropertyName is nameof(AddNewTransactionVM.SelectedPinnedHistoryItem) or
            nameof(AddNewTransactionVM.SelectedHistoryItem))
        {
            SyncNoteDocumentFromViewModel();
            FocusPrimaryInput();
        }
    }

    private void OnMoreTagsButtonChecked(object sender, RoutedEventArgs e) => TryOpenMoreTagsPopup();

    private void OnMoreTagsButtonUnchecked(object sender, RoutedEventArgs e) => TryCloseMoreTagsPopup();

    private void OnMoreTagsHoverChanged(object sender, RoutedEventArgs e)
    {
        if (!CanShowMoreTagsPopup())
        {
            _moreTagsHoverCloseTimer.Stop();
            TryCloseMoreTagsPopup();
            return;
        }

        var isPointerOverMoreRegion = IsPointerOverMoreRegion();
        if (isPointerOverMoreRegion)
        {
            _moreTagsHoverCloseTimer.Stop();

            if (!_viewModel.IsMoreTagsOpen)
                TryOpenMoreTagsPopup();

            return;
        }

        if (_viewModel.IsMoreTagsOpen)
            return;

        _moreTagsHoverCloseTimer.Stop();
        _moreTagsHoverCloseTimer.Start();
    }

    private void OnMoreTagsPopupClosed(object? sender, EventArgs e)
    {
        _moreTagsHoverCloseTimer.Stop();
        _moreTagsPopupState = MoreTagsPopupLifecycleState.Closed;

        if (_viewModel.IsMoreTagsOpen)
            _viewModel.IsMoreTagsOpen = false;
    }

    private void OnPopupPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsMoreTagsOpen || _moreTagsPopupState is not MoreTagsPopupLifecycleState.Open)
            return;

        if (e.OriginalSource is not DependencyObject source)
            return;

        if (DependencyObjectTree.IsDescendantOf(source, MoreTagsButton))
            return;

        _viewModel.IsMoreTagsOpen = false;
        TryCloseMoreTagsPopup();
    }

    private void SyncNameSuggestionsPopupState()
    {
        var shouldShow = IsLoaded && _viewModel.HasTransactionNameSuggestions && !_viewModel.IsGoal;
        var isNameSuggestionFocused =
            ExpenseNameTextBox.IsKeyboardFocusWithin || ExpenseNameSuggestionsListBox.IsKeyboardFocusWithin;

        ExpenseNameSuggestionsPopup.IsOpen = shouldShow && isNameSuggestionFocused;
    }

    private void OnTransactionNameTextBoxFocusChanged(object sender, KeyboardFocusChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(SyncNameSuggestionsPopupState));
    }

    private void OnTransactionNameTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _viewModel.ValidateNameField();
        OnTransactionNameTextBoxFocusChanged(sender, e);
    }

    private void OnTransactionAmountTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _viewModel.ValidateAmountField();
    }

    private void OnTransactionAmountTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox { IsKeyboardFocusWithin: true } textBox)
            return;

        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        _viewModel.ActivateAmountValidation();
    }

    private void RecalculateTagLayout()
    {
        if (!IsLoaded || !_viewModel.IsExpense)
            return;

        var containerWidth = TagsDockPanel.ActualWidth;
        if (containerWidth <= 0)
            return;

        var orderedTags = _viewModel.VisibleTags.Concat(_viewModel.OverflowTags).ToList();
        if (orderedTags.Count == 0)
        {
            _viewModel.SetVisibleTagSlots(0);
            SyncMoreTagsPopupState();
            return;
        }

        var addTagWidth = AddTagButton.ActualWidth + AddTagButton.Margin.Left + AddTagButton.Margin.Right;
        var moreButtonWidth = MeasureMoreButtonWidth();
        var tagWidths = orderedTags.Select(MeasureTagWidth).ToList();
        var visibleSlots = CalculateVisibleTagSlots(containerWidth, addTagWidth, moreButtonWidth, tagWidths);
        _viewModel.SetVisibleTagSlots(visibleSlots);
        SyncMoreTagsPopupState();
    }

    private bool IsPointerOverMoreRegion()
    {
        return MoreTagsButton.IsMouseOver || MoreTagsPopupContent.IsMouseOver;
    }

    private bool CanShowMoreTagsPopup()
    {
        return IsLoaded && _viewModel.HasMoreTags;
    }

    private void SyncMoreTagsPopupState()
    {
        if (!CanShowMoreTagsPopup())
        {
            _viewModel.IsMoreTagsOpen = false;
            TryCloseMoreTagsPopup();
            return;
        }

        if (_viewModel.IsMoreTagsOpen || IsPointerOverMoreRegion())
            TryOpenMoreTagsPopup();
        else
            TryCloseMoreTagsPopup();
    }

    private void TryOpenMoreTagsPopup()
    {
        if (!CanShowMoreTagsPopup())
            return;

        if (_moreTagsPopupState is MoreTagsPopupLifecycleState.Open or MoreTagsPopupLifecycleState.Opening)
            return;

        _moreTagsPopupState = MoreTagsPopupLifecycleState.Opening;
        MoreTagsPopup.IsOpen = true;
        if (MoreTagsPopup.IsOpen)
            _moreTagsPopupState = MoreTagsPopupLifecycleState.Open;
        else
            _moreTagsPopupState = MoreTagsPopupLifecycleState.Closed;
    }

    private void TryCloseMoreTagsPopup()
    {
        if (_moreTagsPopupState is MoreTagsPopupLifecycleState.Closed or MoreTagsPopupLifecycleState.Closing)
            return;

        _moreTagsPopupState = MoreTagsPopupLifecycleState.Closing;
        MoreTagsPopup.IsOpen = false;
        if (!MoreTagsPopup.IsOpen)
            _moreTagsPopupState = MoreTagsPopupLifecycleState.Closed;
    }

    private void TryCloseMoreTagsPopupIfNotPinned()
    {
        if (_viewModel.IsMoreTagsOpen || IsPointerOverMoreRegion())
            return;

        TryCloseMoreTagsPopup();
    }

    private double MeasureTagWidth(TagVM tag)
    {
        var tagChip = new RadioButton
        {
            Content = tag.Name,
            DataContext = tag,
            Style = (Style)FindResource("PopupTagItemRadioStyle")
        };

        tagChip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return tagChip.DesiredSize.Width + 8d;
    }

    private double MeasureMoreButtonWidth()
    {
        var moreButton = new ToggleButton
        {
            Content = "More",
            Style = (Style)FindResource("PopupTagToggleStyle")
        };

        moreButton.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return moreButton.DesiredSize.Width + MoreTagsButton.Margin.Left + MoreTagsButton.Margin.Right;
    }

    private static int CalculateVisibleTagSlots(
        double containerWidth,
        double addTagWidth,
        double moreButtonWidth,
        IReadOnlyList<double> tagWidths)
    {
        var remainingWidth = Math.Max(0d, containerWidth - addTagWidth);
        var totalTagsWidth = tagWidths.Sum();

        if (totalTagsWidth <= remainingWidth)
            return tagWidths.Count;

        var remainingWidthWithMore = Math.Max(0d, remainingWidth - moreButtonWidth);
        if (remainingWidthWithMore <= 0d)
            return 0;

        var consumedWidth = 0d;
        var visibleCount = 0;
        foreach (var width in tagWidths)
        {
            if (consumedWidth + width > remainingWidthWithMore)
                break;

            consumedWidth += width;
            visibleCount++;
        }

        return visibleCount;
    }
}
