using System.Windows;
using System.Windows.Controls;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.Views.CustomControls;
using Fluxo.Views.Popups.Settings;

namespace Fluxo.Views.Popups.Settings.Tabs;

public partial class SettingsTagsTab : UserControl
{
    public SettingsTagsTab()
    {
        InitializeComponent();
    }

    private SettingsTagsTabVM? ViewModel => DataContext as SettingsTagsTabVM;

    private void OnAddTagClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is not SettingsPopup settingsPopup)
            return;

        settingsPopup.OpenAddTagPopup();
    }

    private async void OnTagDeleteClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not FrameworkElement { DataContext: ExpenseTagVM tag })
            return;

        if (FluxoMessageBox.Show(Window.GetWindow(this), $"Delete the tag \"{tag.Name}\"?", "Tags", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var result = await ViewModel.DeleteTagAsync(tag);
        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorMessage))
            FluxoMessageBox.Show(Window.GetWindow(this), result.ErrorMessage, "Tags", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
