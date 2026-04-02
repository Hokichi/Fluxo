using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Fluxo.Views.Components
{
    /// <summary>
    /// Interaction logic for ExpensesList.xaml
    /// </summary>
    public partial class ExpensesList : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(ExpensesList), new PropertyMetadata(default(string)));

        public static readonly DependencyProperty SpentAmountProperty = DependencyProperty.Register(
            nameof(SpentAmount), typeof(int), typeof(ExpensesList), new PropertyMetadata(default(int)));

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource), typeof(ICollectionView), typeof(ExpensesList), new PropertyMetadata(default(ICollectionView)));

        public static readonly DependencyProperty IsListEmptyProperty = DependencyProperty.Register(
            nameof(IsListEmpty), typeof(bool), typeof(ExpensesList), new PropertyMetadata(default(bool)));

        public static readonly DependencyProperty DotColorProperty = DependencyProperty.Register(
            nameof(DotColor), typeof(Brush), typeof(ExpensesList), new PropertyMetadata(null));

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public int SpentAmount
        {
            get { return (int)GetValue(SpentAmountProperty); }
            set { SetValue(SpentAmountProperty, value); }
        }

        public ICollectionView ItemsSource
        {
            get { return (ICollectionView)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public bool IsListEmpty
        {
            get { return (bool)GetValue(IsListEmptyProperty); }
            set { SetValue(IsListEmptyProperty, value); }
        }

        public Brush DotColor
        {
            get { return (Brush)GetValue(DotColorProperty); }
            set { SetValue(DotColorProperty, value); }
        }

        public ExpensesList()
        {
            InitializeComponent();
        }

        private void OnExpenseListPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not DependencyObject source)
                return;

            var fadingScrollViewer = FindAncestor<Resources.CustomControls.FadingScrollViewer>(source);
            if (fadingScrollViewer is null)
                return;

            var lineCount = Math.Max(1, Math.Abs(e.Delta) / Mouse.MouseWheelDeltaForOneLine);
            for (var i = 0; i < lineCount; i++)
            {
                if (e.Delta > 0)
                    fadingScrollViewer.LineUp();
                else
                    fadingScrollViewer.LineDown();
            }

            e.Handled = true;
        }

        private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
        {
            for (DependencyObject? current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is T match)
                    return match;
            }

            return null;
        }
    }
}