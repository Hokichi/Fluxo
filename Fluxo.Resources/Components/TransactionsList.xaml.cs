using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Resources.CustomControls;
using Fluxo.Resources.Infrastructure;
using Fluxo.Resources.Resources.Messages;

namespace Fluxo.Resources.Components;

/// <summary>
///     Interaction logic for TransactionsList.xaml
/// </summary>
public partial class TransactionsList : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(TransactionsList), new PropertyMetadata(default(string)));

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ICollectionView), typeof(TransactionsList),
        new PropertyMetadata(default(ICollectionView), OnItemsSourceChanged));

    public static readonly DependencyProperty MaxVisibleItemsProperty = DependencyProperty.Register(
        nameof(MaxVisibleItems), typeof(int), typeof(TransactionsList),
        new PropertyMetadata(int.MaxValue, OnMaxVisibleItemsChanged));

    private static readonly DependencyPropertyKey VisibleItemsSourcePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(VisibleItemsSource), typeof(IEnumerable), typeof(TransactionsList),
            new PropertyMetadata(Array.Empty<object>()));

    public static readonly DependencyProperty VisibleItemsSourceProperty =
        VisibleItemsSourcePropertyKey.DependencyProperty;

    public static readonly DependencyProperty IsListEmptyProperty = DependencyProperty.Register(
        nameof(IsListEmpty), typeof(bool), typeof(TransactionsList), new PropertyMetadata(default(bool)));

    public static readonly DependencyProperty IsEmptyActionVisibleProperty = DependencyProperty.Register(
        nameof(IsEmptyActionVisible), typeof(bool), typeof(TransactionsList), new PropertyMetadata(true));

    public static readonly DependencyProperty EmptyActionTextProperty = DependencyProperty.Register(
        nameof(EmptyActionText), typeof(string), typeof(TransactionsList), new PropertyMetadata("Add"));

    public static readonly DependencyProperty EmptyActionParameterProperty = DependencyProperty.Register(
        nameof(EmptyActionParameter), typeof(object), typeof(TransactionsList), new PropertyMetadata(default(object)));

    public static readonly DependencyProperty DotColorProperty = DependencyProperty.Register(
        nameof(DotColor), typeof(Brush), typeof(TransactionsList), new PropertyMetadata(null));

    public static readonly DependencyProperty DeleteCommandProperty = DependencyProperty.Register(
        nameof(DeleteCommand), typeof(ICommand), typeof(TransactionsList), new PropertyMetadata(default(ICommand)));

    public static readonly DependencyProperty LoadMoreCommandProperty = DependencyProperty.Register(
        nameof(LoadMoreCommand), typeof(ICommand), typeof(TransactionsList), new PropertyMetadata(default(ICommand)));

    public static readonly DependencyProperty HasMoreItemsProperty = DependencyProperty.Register(
        nameof(HasMoreItems), typeof(bool), typeof(TransactionsList), new PropertyMetadata(default(bool)));

    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading), typeof(bool), typeof(TransactionsList), new PropertyMetadata(default(bool)));

    public TransactionsList()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler? EmptyActionClick;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
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

    public int MaxVisibleItems
    {
        get => (int)GetValue(MaxVisibleItemsProperty);
        set => SetValue(MaxVisibleItemsProperty, value);
    }

    public IEnumerable VisibleItemsSource => (IEnumerable)GetValue(VisibleItemsSourceProperty);

    public bool IsEmptyActionVisible
    {
        get => (bool)GetValue(IsEmptyActionVisibleProperty);
        set => SetValue(IsEmptyActionVisibleProperty, value);
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

    private void OnTransactionDetailButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: { } expenseLog })
            return;

        var rowTransaction = expenseLog.GetType().GetProperty("Transaction")?.GetValue(expenseLog);
        if (rowTransaction is null && expenseLog.GetType().GetProperty("Transaction") is not null)
            return;

        // Reset the swipe on the container
        if (DependencyObjectTree.FindAncestor<SwipeRevealContainer>((DependencyObject)sender) is { } container)
            container.ResetSwipe();

        WindowMethodInvoker.TryInvoke(this, "OpenTransactionDetailPopup", rowTransaction ?? expenseLog);
    }

    private void OnEmptyActionButtonClick(object sender, RoutedEventArgs e)
    {
        EmptyActionClick?.Invoke(this, e);
    }

    private void OnViewInLedgerButtonClick(object sender, RoutedEventArgs e)
    {
        RequestLedgerNavigation(WeakReferenceMessenger.Default);
    }

    internal static void RequestLedgerNavigation(IMessenger messenger)
    {
        messenger.Send(new NavigateToLedgerRequestedMessage());
    }

    private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: { } row })
            ExecuteDelete(DeleteCommand, row);
    }

    internal static void ExecuteDelete(ICommand? command, object row)
    {
        if (command?.CanExecute(row) == true)
            command.Execute(row);
    }

    private static void OnItemsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var control = (TransactionsList)dependencyObject;

        if (e.OldValue is ICollectionView oldView)
            oldView.CollectionChanged -= control.OnItemsSourceCollectionChanged;

        if (e.NewValue is ICollectionView newView)
            newView.CollectionChanged += control.OnItemsSourceCollectionChanged;

        control.RefreshVisibleItems();
    }

    private static void OnMaxVisibleItemsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((TransactionsList)dependencyObject).RefreshVisibleItems();
    }

    private void OnItemsSourceCollectionChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        RefreshVisibleItems();
    }

    private void RefreshVisibleItems()
    {
        SetValue(VisibleItemsSourcePropertyKey, LimitItems(ItemsSource, MaxVisibleItems));
    }

    internal static IReadOnlyList<object> LimitItems(IEnumerable? items, int limit)
    {
        return items?.Cast<object>().Take(Math.Max(0, limit)).ToArray() ?? [];
    }

    private void OnTransactionsListPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject source)
            return;

        var fadingScrollViewer = DependencyObjectTree.FindAncestor<FadingScrollViewer>(source);
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

}
