using System.Windows;
using System.Windows.Controls;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.Services.Dialogs;

namespace Fluxo.Views.Popups;

public partial class NotificationChecklistActionPopup : BasePopup
{
    private readonly IDialogService _dialogService;
    private readonly SettingsTagsTabVM _settingsTagsTabViewModel;
    private readonly NotificationChecklistActionVM _viewModel;

    public NotificationChecklistActionPopup(
        NotificationChecklistActionVM viewModel,
        IDialogService dialogService,
        SettingsTagsTabVM settingsTagsTabViewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _dialogService = dialogService;
        _settingsTagsTabViewModel = settingsTagsTabViewModel;
        DataContext = _viewModel;
    }

    private async void OnAddTagClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { TemplatedParent: ComboBox comboBox })
            comboBox.IsDropDownOpen = false;

        _dialogService.ShowAddTag(_settingsTagsTabViewModel, this);
        await _viewModel.RefreshAvailableTagsAsync();
    }
}
