using System;
using System.IO;
using System.Windows;
using Fluxo.Core.Enums;
using Fluxo.Services.Logging;
using Fluxo.ViewModels.Popups.DataManagement;
using Microsoft.Win32;

namespace Fluxo.Views.Popups;

public partial class DataManagementPopup : BasePopup
{
    private readonly DataManagementVM _viewModel;

    public DataManagementPopup(DataManagementVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnBackupModeClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Mode = DataManagementMode.Backup;
    }

    private void OnAppendModeClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Mode = DataManagementMode.Append;
    }

    private void OnOverwriteModeClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Mode = DataManagementMode.Overwrite;
    }

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Mode == DataManagementMode.Backup)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                FileName = Path.GetFileName(_viewModel.FilePath),
                InitialDirectory = _viewModel.GetInitialDirectory()
            };

            if (dialog.ShowDialog(this) == true)
                _viewModel.FilePath = dialog.FileName;

            return;
        }

        var openDialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            InitialDirectory = _viewModel.GetInitialDirectory()
        };

        if (openDialog.ShowDialog(this) == true)
        {
            try
            {
                await _viewModel.LoadManifestAsync(openDialog.FileName);
            }
            catch (Exception exception)
            {
                FluxoLogManager.LogError(exception, "Unable to read backup manifest.");
                FluxoMessageBox.Show(
                    this,
                    FluxoLogManager.CreateFailureMessage("read backup file"),
                    "Data Management",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    private async void OnStartClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.StartAsync();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
