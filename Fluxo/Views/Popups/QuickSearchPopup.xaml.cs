using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.Resources.CustomControls;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.Shell;

namespace Fluxo.Views.Popups;

public partial class QuickSearchPopup : BasePopup
{
    private readonly IReadOnlyList<ExpenseLogVM> _allExpenseLogs;
    private readonly ObservableCollection<ExpenseLogVM> _searchResults = [];

    public QuickSearchPopup(MainVM mainVM)
    {
        InitializeComponent();
        _allExpenseLogs = mainVM.GetAllExpenseLogs();
        ResultsList.ItemsSource = _searchResults;

        Loaded += (_, _) => SearchBox.Focus();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text?.Trim();
        _searchResults.Clear();

        if (string.IsNullOrEmpty(query) || query.Length <= 3)
        {
            ResultsBorder.Visibility = Visibility.Collapsed;
            NoResultsText.Visibility = Visibility.Collapsed;
            return;
        }

        var matches = _allExpenseLogs
            .Where(log => log.Expense?.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            .Take(5)
            .ToList();

        ResultsBorder.Visibility = Visibility.Visible;

        if (matches.Count == 0)
        {
            NoResultsText.Visibility = Visibility.Visible;
            return;
        }

        NoResultsText.Visibility = Visibility.Collapsed;

        foreach (var match in matches)
            _searchResults.Add(match);
    }

    private void OnResultItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ExpenseLogVM log })
            return;

        var mainWindow = Owner as MainWindow;
        Close();

        mainWindow?.OpenExpenseDetailPopup(log);
    }
}