using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using Fluxo.Services.Dialogs;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.ViewModels.Popups.Helpers;

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
    private readonly QuickAddVM _viewModel;
    private bool _isHandlingAddTagSelection;
    private readonly DispatcherTimer _moreTagsHoverCloseTimer;
    private MoreTagsPopupLifecycleState _moreTagsPopupState = MoreTagsPopupLifecycleState.Closed;
    private bool _isSyncingNoteDocument;

    public AddNewTransaction(
        QuickAddVM viewModel,
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
            RecalculateTagLayout();
            SyncMoreTagsPopupState();
            SyncNoteDocumentFromViewModel();
            _viewModel.BeginChangeTracking();
            FocusPrimaryInput();
        };

        TagsDockPanel.SizeChanged += (_, _) => RecalculateTagLayout();
        PreviewMouseDown += OnPopupPreviewMouseDown;
    }

    protected override async void OnSaveButtonClick()
    {
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
        var result = await _viewModel.SaveAsync(true);
        if (!result.IsSuccess)
        {
            ShowValidationMessage(result.ErrorMessage);
            return;
        }

        NoteRichTextBox.Document.Blocks.Clear();
        _viewModel.BeginChangeTracking();
        FocusPrimaryInput();
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

        _viewModel.NoteText = new TextRange(NoteRichTextBox.Document.ContentStart, NoteRichTextBox.Document.ContentEnd)
            .Text
            .Trim();
    }

    private void ShowValidationMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        FluxoMessageBox.Show(this, message, "Add New Transaction", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void FocusPrimaryInput()
    {
        if (_viewModel.IsGoal)
        {
            GoalAmountTextBox.Focus();
            return;
        }

        if (_viewModel.IsExpense)
        {
            ExpenseAmountTextBox.Focus();
            return;
        }

        IncomeAmountTextBox.Focus();
    }

    private void SyncNoteDocumentFromViewModel()
    {
        _isSyncingNoteDocument = true;

        try
        {
            var noteText = _viewModel.NoteText ?? string.Empty;
            new TextRange(NoteRichTextBox.Document.ContentStart, NoteRichTextBox.Document.ContentEnd).Text = noteText;
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

        if (IsDescendantOf(source, MoreTagsButton))
            return;

        _viewModel.IsMoreTagsOpen = false;
        TryCloseMoreTagsPopup();
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

    private static bool IsDescendantOf(DependencyObject source, DependencyObject ancestor)
    {
        var current = source;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
                return true;

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
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

    private double MeasureTagWidth(ExpenseTagVM tag)
    {
        var tagChip = new ToggleButton
        {
            Content = tag.Name,
            DataContext = tag,
            Style = (Style)FindResource("PopupTagItemToggleStyle")
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

    private void OnRecurringDatePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
            e.Handled = true;
    }

    private void OnRecurringDatePreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox textBox)
            e.Handled = !WouldResultInValidRecurringDate(textBox, e.Text);
    }

    private void OnRecurringDatePasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
        if (!WouldResultInValidRecurringDate(textBox, pastedText))
            e.CancelCommand();
    }

    private static bool WouldResultInValidRecurringDate(TextBox textBox, string incomingText)
    {
        var candidateText = BuildCandidateText(textBox, incomingText);
        if (string.IsNullOrWhiteSpace(candidateText))
            return true;

        if (!int.TryParse(candidateText, out var value))
            return false;

        return value is >= MonthlyDueDateHelper.MinMonthlyDay and <= MonthlyDueDateHelper.MaxMonthlyDay;
    }

    private static string BuildCandidateText(TextBox textBox, string incomingText)
    {
        var currentText = textBox.Text ?? string.Empty;
        var selectionStart = textBox.SelectionStart;
        var selectionLength = textBox.SelectionLength;
        return currentText.Remove(selectionStart, selectionLength).Insert(selectionStart, incomingText);
    }

}
