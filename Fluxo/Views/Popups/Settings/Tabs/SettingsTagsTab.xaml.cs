using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.Views.Popups.Settings.Tabs;

public partial class SettingsTagsTab : UserControl
{
    public SettingsTagsTab()
    {
        InitializeComponent();
    }

    private SettingsTagsTabVM? _viewModel => DataContext as SettingsTagsTabVM;

    private void OnAddTagClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.RequestAddTagDialog();
    }

    private async void OnTagPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null ||
            sender is not FrameworkElement { DataContext: ExpenseTagVM tag })
            return;

        var isDotClick = IsDotClick(sender, e);
        if (!isDotClick || e.ClickCount != 1)
            return;

        e.Handled = true;

        if (FluxoMessageBox.Show(Window.GetWindow(this), $"Delete the tag \"{tag.Name}\"?", "Tags", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var deleteResult = await _viewModel.DeleteTagAsync(tag);
        if (!deleteResult.IsSuccess && !string.IsNullOrWhiteSpace(deleteResult.ErrorMessage))
        {
            FluxoMessageBox.Show(Window.GetWindow(this), deleteResult.ErrorMessage, "Tags",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void OnTagMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null ||
            sender is not FrameworkElement { DataContext: ExpenseTagVM tag } ||
            IsDotClick(sender, e))
            return;

        e.Handled = true;
        await _viewModel.OpenEditTagAsync(tag.Id);
    }

    private static bool IsDotClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return false;

        var clickPosition = e.GetPosition(element);

        // Matches the chip geometry in TagDeleteButtonStyle:
        // left padding (12) + dot width (10) + a small buffer.
        const double dotHitMaxX = 26d;
        return clickPosition.X <= dotHitMaxX;
    }
}
