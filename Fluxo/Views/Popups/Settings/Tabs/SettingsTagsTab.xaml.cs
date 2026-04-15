using System.Windows;
using System.Windows.Controls;
using Fluxo.Resources.CustomControls;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.Views.Popups;

namespace Fluxo.Views.Popups.Settings.Tabs;

public partial class SettingsTagsTab : UserControl
{
    public SettingsTagsTab()
    {
        InitializeComponent();
    }

    private SettingsVM? ViewModel => DataContext as SettingsVM;

    private void OnAddTagClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        new AddTagPopup(ViewModel) { Owner = Window.GetWindow(this) }.ShowDialog();
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
