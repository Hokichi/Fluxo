using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Fluxo.Resources.CustomControls;
using Fluxo.Resources.Infrastructure;

namespace Fluxo.Resources.Components;

/// <summary>
///     Interaction logic for ExpensesList.xaml
/// </summary>
public partial class ExpensesList : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ExpensesList), new PropertyMetadata(default(string)));

    public static readonly DependencyProperty SpentAmountProperty = DependencyProperty.Register(
        nameof(SpentAmount), typeof(decimal), typeof(ExpensesList), new PropertyMetadata(default(decimal)));

    public static readonly DependencyProperty TotalAmountProperty = DependencyProperty.Register(
        nameof(TotalAmount), typeof(decimal), typeof(ExpensesList), new PropertyMetadata(default(decimal)));

    public static readonly DependencyProperty RemainingAmountProperty = DependencyProperty.Register(
        nameof(RemainingAmount), typeof(decimal), typeof(ExpensesList), new PropertyMetadata(default(decimal)));

    public static readonly DependencyProperty AllocationProperty = DependencyProperty.Register(
        nameof(Allocation), typeof(int), typeof(ExpensesList), new PropertyMetadata(default(int)));

    public static readonly DependencyProperty PercentageProperty = DependencyProperty.Register(
        nameof(Percentage), typeof(int), typeof(ExpensesList), new PropertyMetadata(default(int)));

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ICollectionView), typeof(ExpensesList),
        new PropertyMetadata(default(ICollectionView)));

    public static readonly DependencyProperty IsListEmptyProperty = DependencyProperty.Register(
        nameof(IsListEmpty), typeof(bool), typeof(ExpensesList), new PropertyMetadata(default(bool)));

    public static readonly DependencyProperty EmptyActionTextProperty = DependencyProperty.Register(
        nameof(EmptyActionText), typeof(string), typeof(ExpensesList), new PropertyMetadata("Add"));

    public static readonly DependencyProperty EmptyActionParameterProperty = DependencyProperty.Register(
        nameof(EmptyActionParameter), typeof(object), typeof(ExpensesList), new PropertyMetadata(default(object)));

    public static readonly DependencyProperty DotColorProperty = DependencyProperty.Register(
        nameof(DotColor), typeof(Brush), typeof(ExpensesList), new PropertyMetadata(null));

    public static readonly DependencyProperty DeleteCommandProperty = DependencyProperty.Register(
        nameof(DeleteCommand), typeof(ICommand), typeof(ExpensesList), new PropertyMetadata(default(ICommand)));

    public static readonly DependencyProperty LoadMoreCommandProperty = DependencyProperty.Register(
        nameof(LoadMoreCommand), typeof(ICommand), typeof(ExpensesList), new PropertyMetadata(default(ICommand)));

    public static readonly DependencyProperty HasMoreItemsProperty = DependencyProperty.Register(
        nameof(HasMoreItems), typeof(bool), typeof(ExpensesList), new PropertyMetadata(default(bool)));

    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading), typeof(bool), typeof(ExpensesList), new PropertyMetadata(default(bool)));

    public ExpensesList()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler? EmptyActionClick;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public decimal SpentAmount
    {
        get => (decimal)GetValue(SpentAmountProperty);
        set => SetValue(SpentAmountProperty, value);
    }

    public decimal TotalAmount
    {
        get => (decimal)GetValue(TotalAmountProperty);
        set => SetValue(TotalAmountProperty, value);
    }


    public decimal RemainingAmount
    {
        get => (decimal)GetValue(RemainingAmountProperty);
        set => SetValue(RemainingAmountProperty, value);
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

    public string EmptyActionText
    {
        get => (string)GetValue(EmptyActionTextProperty);
        set => SetValue(EmptyActionTextProperty, value);
    }

    public object? EmptyActionParameter
    {
        get => GetValue(EmptyActionParameterProperty);
        set => SetValue(EmptyActionParameterProperty, value);
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

    public ICommand LoadMoreCommand
    {
        get => (ICommand)GetValue(LoadMoreCommandProperty);
        set => SetValue(LoadMoreCommandProperty, value);
    }

    public bool HasMoreItems
    {
        get => (bool)GetValue(HasMoreItemsProperty);
        set => SetValue(HasMoreItemsProperty, value);
    }

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    private void OnExpenseDetailButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: { } expenseLog })
            return;

        // Reset the swipe on the container
        if (FindAncestor<SwipeRevealContainer>((DependencyObject)sender) is { } container)
            container.ResetSwipe();

        WindowMethodInvoker.TryInvoke(this, "OpenExpenseDetailPopup", expenseLog);
    }

    private void OnEmptyActionButtonClick(object sender, RoutedEventArgs e)
    {
        EmptyActionClick?.Invoke(this, e);
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
