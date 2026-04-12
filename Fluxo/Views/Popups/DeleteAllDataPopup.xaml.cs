using System.Windows;
using Fluxo.Resources.CustomControls;

namespace Fluxo.Views.Popups;

public partial class DeleteAllDataPopup : BasePopup
{
    public DeleteAllDataPopup()
    {
        InitializeComponent();
    }

    public DeleteAllDataChoice Choice { get; private set; } = DeleteAllDataChoice.Cancel;

    private void OnKeepSettingsClick(object sender, RoutedEventArgs e)
    {
        Choice = DeleteAllDataChoice.KeepSettings;
        DialogResult = true;
        Close();
    }

    private void OnRemoveSettingsClick(object sender, RoutedEventArgs e)
    {
        Choice = DeleteAllDataChoice.RemoveSettings;
        DialogResult = true;
        Close();
    }

    private void OnCancelDeletionClick(object sender, RoutedEventArgs e)
    {
        Choice = DeleteAllDataChoice.Cancel;
        DialogResult = false;
        Close();
    }
}

public enum DeleteAllDataChoice
{
    KeepSettings = 1,
    RemoveSettings = 2,
    Cancel = 3
}
