using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.Resources.CustomControls;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;

namespace Fluxo.Views.Popups;

public partial class SpendingSourceDetailPopup : BasePopup
{
    private readonly SpendingSourceDetailVM _viewModel;
    private bool _allowClose;
    private bool _isHandlingCloseRequest;

    public SpendingSourceDetailPopup(SpendingSourceDetailVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Closing += OnPopupClosing;
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        if (!await _viewModel.LoadAsync())
        {
            FluxoMessageBox.Show(this, "This spending source could not be loaded.", "Income Detail",
                MessageBoxButton.OK, MessageBoxImage.Information);
            _allowClose = true;
            Close();
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

    private async void OnDisableButtonClick(object sender, RoutedEventArgs e)
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
            ShowOnUI = _viewModel.ShowOnUI
        }, _viewModel.UnitOfWork);

        var popup = new TransferFundsPopup(transferVm) { Owner = this };
        popup.ShowDialog();
        _ = _viewModel.LoadAsync();
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
            Close();
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
            Close();
        }
        finally
        {
            _isHandlingCloseRequest = false;
        }
    }

    private void OnAmountTextBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        e.Handled = !IsValidAmountInput(textBox, e.Text);
    }

    private void ShowValidationMessage(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            FluxoMessageBox.Show(this, message, "Income Detail", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static bool IsValidAmountInput(TextBox textBox, string newText)
    {
        var proposedText = GetProposedText(textBox, newText);
        if (string.IsNullOrWhiteSpace(proposedText))
            return true;

        var separators = new HashSet<char> { '.', CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0] };
        var separatorCount = 0;
        foreach (var character in proposedText)
        {
            if (char.IsDigit(character))
                continue;
            if (!separators.Contains(character))
                return false;
            if (++separatorCount > 1)
                return false;
        }

        return true;
    }

    private static string GetProposedText(TextBox textBox, string newText)
    {
        var existingText = textBox.Text ?? string.Empty;
        return textBox.SelectionLength > 0
            ? existingText.Remove(textBox.SelectionStart, textBox.SelectionLength).Insert(textBox.SelectionStart, newText)
            : existingText.Insert(textBox.SelectionStart, newText);
    }
}
