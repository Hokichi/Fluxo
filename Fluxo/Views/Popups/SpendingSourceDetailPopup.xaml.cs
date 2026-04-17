using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.Resources.CustomControls;
using Fluxo.Services.Dialogs;
using Fluxo.ViewModels.Helpers;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.Views.Shell;
using Fluxo.Views.Shell.Main;

namespace Fluxo.Views.Popups;

public partial class SpendingSourceDetailPopup : BasePopup
{
    private readonly IDialogService _dialogService;
    private readonly SpendingSourceDetailVM _viewModel;
    private bool _allowClose;
    private bool _isHandlingCloseRequest;
    private bool _reopenSourcesOnClose;

    public SpendingSourceDetailPopup(SpendingSourceDetailVM viewModel, IDialogService dialogService)
    {
        InitializeComponent();
        _dialogService = dialogService;
        _viewModel = viewModel;
        DataContext = viewModel;
        Closing += OnPopupClosing;
        Closed += OnPopupClosed;
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        if (!await _viewModel.LoadAsync())
        {
            FluxoMessageBox.Show(this, "This spending source could not be loaded.", "Income Detail",
                MessageBoxButton.OK, MessageBoxImage.Information);
            _allowClose = true;
            _ = Dispatcher.BeginInvoke(new System.Action(Close));
            return;
        }

        NameTextBox.Focus();
    }

    private async void OnEditButtonClick(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsEditing)
        {
            _viewModel.BeginEditing();
            NameTextBox.Focus();
            NameTextBox.SelectAll();
            return;
        }

        var result = await _viewModel.SaveAsync();
        if (!result.IsSuccess)
            ShowValidationMessage(result.ErrorMessage);
    }

    private void OnHeaderNameTextMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2 || _viewModel.IsEditing)
            return;

        BeginInlineHeaderEditing(NameTextBox);
        e.Handled = true;
    }

    private void OnHeaderAmountTextMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2 || _viewModel.IsEditing)
            return;

        BeginInlineHeaderEditing(PrimaryAmountTextBox);
        e.Handled = true;
    }

    private void OnCreditSpentTextMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2 || _viewModel.IsEditing)
            return;

        BeginInlineHeaderEditing(CreditSpentAmountTextBox);
        e.Handled = true;
    }

    private void OnCreditLimitTextMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2 || _viewModel.IsEditing)
            return;

        BeginInlineHeaderEditing(CreditLimitTextBox);
        e.Handled = true;
    }

    private void OnBnplSpentTextMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2 || _viewModel.IsEditing)
            return;

        BeginInlineHeaderEditing(BnplSpentAmountTextBox);
        e.Handled = true;
    }

    private void OnSavingApyTextMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2 || _viewModel.IsEditing)
            return;

        BeginInlineHeaderEditing(SavingApyTextBox);
        e.Handled = true;
    }

    private async void OnDisableButtonClick(object sender, RoutedEventArgs e)
    {
        var result = await _viewModel.ToggleEnabledAsync();
        if (!result.IsSuccess)
            ShowValidationMessage(result.ErrorMessage);
    }

    private async void OnHideOrUnhideButtonClick(object sender, RoutedEventArgs e)
    {
        var result = await _viewModel.ToggleVisibilityAsync();
        if (!result.IsSuccess)
            ShowValidationMessage(result.ErrorMessage);
    }

    private void OnTransferButtonClick(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanTransfer)
            return;

        var transferVm = new TransferFundsVM(_viewModel.MainViewModel, new SpendingSourceVM
        {
            Id = _viewModel.SpendingSourceId,
            Name = _viewModel.NameText,
            SpendingSourceType = _viewModel.SpendingSourceType,
            IsEnabled = _viewModel.IsEnabled,
            ShowOnUI = _viewModel.ShowOnUI
        }, _viewModel.UnitOfWork);

        _dialogService.ShowTransferFunds(transferVm, this);
        _ = _viewModel.LoadAsync();
    }

    private void OnBackToSourcesButtonClick(object sender, RoutedEventArgs e)
    {
        if (Owner is not MainWindow)
            return;

        _reopenSourcesOnClose = true;
        Close();
    }

    private async void OnDeleteButtonClick(object sender, RoutedEventArgs e)
    {
        if (FluxoMessageBox.Show(this, "Delete this spending source?", "Income Detail", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var result = await _viewModel.DeleteAsync();
        if (!result.IsSuccess)
        {
            ShowValidationMessage(result.ErrorMessage);
            return;
        }

        if (result.ShouldClose)
        {
            _allowClose = true;
            _ = Dispatcher.BeginInvoke(new System.Action(Close));
        }
    }

    private async void OnPopupClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || _isHandlingCloseRequest || !_viewModel.HasValidChangesToPersistOnClose())
            return;

        e.Cancel = true;
        _isHandlingCloseRequest = true;

        try
        {
            if (FluxoMessageBox.Show(this, "Save your changes before closing?", "Income Detail",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var result = await _viewModel.SaveAsync();
                if (!result.IsSuccess)
                {
                    ShowValidationMessage(result.ErrorMessage);
                    return;
                }
            }

            _allowClose = true;
            _ = Dispatcher.BeginInvoke(new System.Action(Close));
        }
        finally
        {
            _isHandlingCloseRequest = false;
        }
    }

    private void ShowValidationMessage(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            FluxoMessageBox.Show(this, message, "Income Detail", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (!_reopenSourcesOnClose || Owner is not MainWindow ownerWindow)
            return;

        _reopenSourcesOnClose = false;
        ownerWindow.Dispatcher.BeginInvoke(new Action(ownerWindow.OpenSpendingSourcesListPopup));
    }

    private void BeginInlineHeaderEditing(TextBox targetTextBox)
    {
        if (!_viewModel.IsEditing)
            _viewModel.BeginEditing();

        targetTextBox.Focus();
        targetTextBox.CaretIndex = targetTextBox.Text?.Length ?? 0;
        targetTextBox.SelectionLength = 0;
    }

    private void OnMonthlyDueDatePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
            e.Handled = true;
    }

    private void OnMonthlyDueDatePreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox textBox)
            e.Handled = !WouldResultInValidMonthlyDueDate(textBox, e.Text);
    }

    private void OnMonthlyDueDatePasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        if (!e.SourceDataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.SourceDataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        if (!WouldResultInValidMonthlyDueDate(textBox, pastedText))
            e.CancelCommand();
    }

    private static bool WouldResultInValidMonthlyDueDate(TextBox textBox, string incomingText)
    {
        if (string.IsNullOrEmpty(incomingText))
            return true;

        foreach (var character in incomingText)
            if (!char.IsDigit(character))
                return false;

        var currentText = textBox.Text ?? string.Empty;
        var nextText = currentText
            .Remove(textBox.SelectionStart, textBox.SelectionLength)
            .Insert(textBox.SelectionStart, incomingText);

        if (nextText.Length == 0)
            return true;

        if (!int.TryParse(nextText, out var monthlyDueDate))
            return false;

        return monthlyDueDate is >= MonthlyDueDateHelper.MinMonthlyDay and <= MonthlyDueDateHelper.MaxMonthlyDay;
    }
}
