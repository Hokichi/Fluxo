using System.Windows;
using System.Windows.Controls;
using Fluxo.Core.Enums;
using Fluxo.Resources.CustomControls;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Views.Shell.Main.Controls;

public partial class MainViewModeToggleControl : UserControl
{
    public MainViewModeToggleControl()
    {
        InitializeComponent();
    }

    private async void OnViewModeButtonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModeToggleVM viewModel)
            return;

        if (sender is not SegmentedToggleOption { CommandParameter: MainContentViewMode viewMode })
            return;

        await viewModel.SetSelectedMainContentViewFromUserAsync(viewMode, Window.GetWindow(this));
    }
}
