using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Fluxo.ViewModels.Entities;
using Fluxo.Views.CustomControls;
using Fluxo.Views.Shell.Main;

namespace Fluxo.Views.Components;

/// <summary>
///     Interaction logic for ExpensesList.xaml
/// </summary>
public partial class ExpensesList : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ExpensesList), new PropertyMetadata(default(string)));

    public static readonly DependencyProperty SpentAmountProperty = DependencyProperty.Register(
        nameof(SpentAmount), typeof(int), typeof(ExpensesList), new PropertyMetadata(default(int)));

    public static readonly DependencyProperty TotalAmountProperty = DependencyProperty.Register(
        nameof(TotalAmount), typeof(int), typeof(ExpensesList), new PropertyMetadata(default(int)));

    public static readonly DependencyProperty AllocationProperty = DependencyProperty.Register(
        nameof(Allocation), typeof(int), typeof(ExpensesList), new PropertyMetadata(default(int)));

    public static readonly DependencyProperty PercentageProperty = DependencyProperty.Register(
        nameof(Percentage), typeof(int), typeof(ExpensesList), new PropertyMetadata(default(int)));

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ICollectionView), typeof(ExpensesList),
        new PropertyMetadata(default(ICollectionView)));

    public static readonly DependencyProperty IsListEmptyProperty = DependencyProperty.Register(
        nameof(IsListEmpty), typeof(bool), typeof(ExpensesList), new PropertyMetadata(default(bool)));

    public static readonly DependencyProperty DotColorProperty = DependencyProperty.Register(
        nameof(DotColor), typeof(Brush), typeof(ExpensesList), new PropertyMetadata(null));

    public static readonly DependencyProperty DeleteCommandProperty = DependencyProperty.Register(
        nameof(DeleteCommand), typeof(ICommand), typeof(ExpensesList), new PropertyMetadata(default(ICommand)));

    public ExpensesList()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public int SpentAmount
    {
        get => (int)GetValue(SpentAmountProperty);
        set => SetValue(SpentAmountProperty, value);
    }

    public int TotalAmount
    {
        get => (int)GetValue(TotalAmountProperty);
        set => SetValue(TotalAmountProperty, value);
    }

    public int Allocation
    {
        get => (int)GetValue(AllocationProperty);
        set => SetValue(AllocationProperty, value);
    }

    public int Percentage
    {
        get => (int)GetValue(PercentageProperty);
        set => SetValue(PercentageProperty, value);
    }

    public ICollectionView ItemsSource
    {
        get => (ICollectionView)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public bool IsListEmpty
    {
        get => (bool)GetValue(IsListEmptyProperty);
        set => SetValue(IsListEmptyProperty, value);
    }

    public Brush DotColor
    {
        get => (Brush)GetValue(DotColorProperty);
        set => SetValue(DotColorProperty, value);
    }

    public ICommand DeleteCommand
    {
        get => (ICommand)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    private void OnExpenseDetailButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ExpenseLogVM expenseLog })
            return;

        var mainWindow = Window.GetWindow(this);

        // Reset the swipe on the container
        if (FindAncestor<SwipeRevealContainer>((DependencyObject)sender) is { } container)
            container.ResetSwipe();

        if (mainWindow is MainWindow ownerWindow)
            ownerWindow.OpenExpenseDetailPopup(expenseLog);
    }

    private void OnExpenseListPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject source)
            return;

        var fadingScrollViewer = FindAncestor<FadingScrollViewer>(source);
        if (fadingScrollViewer is null)
            return;

        var lineCount = Math.Max(1, Math.Abs(e.Delta) / Mouse.MouseWheelDeltaForOneLine);
        for (var i = 0; i < lineCount; i++)
            if (e.Delta > 0)
                fadingScrollViewer.LineUp();
            else
                fadingScrollViewer.LineDown();

        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is T match)
                return match;

        return null;
    }
}
