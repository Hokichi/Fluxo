using System.Windows;
using System.Windows.Controls;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.Views.Popups.Settings.Tabs;

public partial class SettingsTagsTab : UserControl
{
    public SettingsTagsTab()
    {
        InitializeComponent();
    }

    private SettingsTagsTabVM? _viewModel => DataContext as SettingsTagsTabVM;

    private void OnAddTagClick(object sender, RoutedEventArgs e) =>
        _viewModel?.RequestAddTagDialog();

    private async void OnEditTagClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not FrameworkElement { DataContext: SettingsTagCardVM tag })
            return;

        await _viewModel.OpenEditTagAsync(tag.Id);
    }

    private async void OnDeleteTagClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not FrameworkElement { DataContext: SettingsTagCardVM tag })
            return;

        if (FluxoMessageBox.Show(Window.GetWindow(this), $"Delete the tag \"{tag.Name}\"?", "Tags",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var result = await _viewModel.DeleteTagAsync(tag);
        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            FluxoMessageBox.Show(Window.GetWindow(this), result.ErrorMessage, "Tags",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
