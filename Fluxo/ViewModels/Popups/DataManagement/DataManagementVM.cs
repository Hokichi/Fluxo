using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Services.Logging;

namespace Fluxo.ViewModels.Popups.DataManagement;

public partial class DataManagementVM : ObservableObject
{
    private readonly IUserBackupService _backupService;
    private readonly HashSet<DataManagementEntityKind> _manifestAvailableEntities = [];

    [ObservableProperty] private DataManagementMode _mode = DataManagementMode.Backup;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private int _pageIndex;
    [ObservableProperty] private string _operationTitle = string.Empty;
    [ObservableProperty] private string _operationDescription = string.Empty;
    [ObservableProperty] private string _resultMessage = string.Empty;
    [ObservableProperty] private bool _isResultSuccess;

    public DataManagementVM(IUserBackupService backupService)
    {
        _backupService = backupService;
        FilePath = backupService.BuildDefaultBackupPath(DateTime.Now);

        Entities =
        [
            new(DataManagementEntityKind.Expenses, "Expenses"),
            new(DataManagementEntityKind.Incomes, "Incomes"),
            new(DataManagementEntityKind.SpendingSources, "Spending Sources"),
            new(DataManagementEntityKind.Tags, "Tags"),
            new(DataManagementEntityKind.Goals, "Goals"),
            new(DataManagementEntityKind.RecurringTransactions, "Recurring Transactions"),
            new(DataManagementEntityKind.UserSettings, "User Settings")
        ];

        GetEntity(DataManagementEntityKind.SpendingSources).PropertyChanged += OnSpendingSourcePropertyChanged;

        _manifestAvailableEntities.UnionWith(Entities.Select(entity => entity.EntityKind));
    }

    public ObservableCollection<DataManagementEntityOptionVM> Entities { get; }
    public ObservableCollection<DataManagementConflictItemVM> Conflicts { get; } = [];

    public bool IsBackupSelected => Mode == DataManagementMode.Backup;
    public bool IsAppendSelected => Mode == DataManagementMode.Append;
    public bool IsOverwriteSelected => Mode == DataManagementMode.Overwrite;

    public DataManagementEntityOptionVM GetEntity(DataManagementEntityKind entityKind) =>
        Entities.Single(entity => entity.EntityKind == entityKind);

    public void SetEntityChecked(DataManagementEntityKind entityKind, bool isChecked)
    {
        GetEntity(entityKind).IsChecked = isChecked;
        ApplyDependencyRules();
    }

    public void ApplyManifest(UserBackupManifest manifest)
    {
        _manifestAvailableEntities.Clear();
        _manifestAvailableEntities.UnionWith(manifest.IncludedEntities);

        foreach (var entity in Entities)
        {
            var available = _manifestAvailableEntities.Contains(entity.EntityKind);
            entity.IsEnabled = available;
            entity.IsChecked = available;
        }

        ApplyDependencyRules();
    }

    public UserBackupSelection BuildSelection()
    {
        return new UserBackupSelection(Entities
            .Where(entity => entity.IsEnabled && entity.IsChecked)
            .Select(entity => entity.EntityKind)
            .ToHashSet());
    }

    public IReadOnlyDictionary<string, DataManagementConflictDecision> BuildConflictDecisions()
    {
        return Conflicts.ToDictionary(conflict => conflict.ConflictKey, conflict => conflict.Decision);
    }

    public string GetInitialDirectory()
    {
        var directory = Path.GetDirectoryName(FilePath);
        return string.IsNullOrWhiteSpace(directory)
            ? _backupService.GetDefaultBackupDirectory()
            : directory;
    }

    public async Task LoadManifestAsync(string filePath)
    {
        var manifest = await _backupService.ReadManifestAsync(filePath);
        FilePath = filePath;
        Conflicts.Clear();
        ApplyManifest(manifest);
        PageIndex = 0;
    }

    [RelayCommand]
    public async Task StartAsync()
    {
        try
        {
            PageIndex = 1;
            SetOperationCopy();

            var selection = BuildSelection();
            UserBackupOperationResult result;

            if (Mode == DataManagementMode.Backup)
            {
                result = await _backupService.BackupAsync(selection, FilePath);
                ShowResult(result);
                return;
            }

            if (Mode == DataManagementMode.Append)
            {
                var conflicts = await _backupService.FindAppendConflictsAsync(FilePath, selection);
                if (conflicts.Count > 0 && Conflicts.Count == 0)
                {
                    Conflicts.Clear();
                    foreach (var conflict in conflicts)
                        Conflicts.Add(new DataManagementConflictItemVM(conflict));
                    PageIndex = 2;
                    return;
                }

                result = await _backupService.AppendAsync(FilePath, selection, BuildConflictDecisions());
                ShowResult(result);
                return;
            }

            result = await _backupService.OverwriteAsync(FilePath, selection);
            ShowResult(result);
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Data management operation failed.");
            ShowResult(UserBackupOperationResult.Failure(exception.Message));
        }
    }

    private void SetOperationCopy()
    {
        OperationTitle = Mode switch
        {
            DataManagementMode.Backup => "Backing up your data",
            DataManagementMode.Append => "Appending backup data",
            DataManagementMode.Overwrite => "Restoring backup data",
            _ => "Processing data"
        };
        OperationDescription = "This will only take a moment.";
    }

    private void ShowResult(UserBackupOperationResult result)
    {
        IsResultSuccess = result.IsSuccess;
        ResultMessage = (Mode, result.IsSuccess) switch
        {
            (DataManagementMode.Backup, true) => "Backup is ready",
            (DataManagementMode.Backup, false) => $"Backup has not been created. Please refer to {ResolveLogFileName()} for more details.",
            (DataManagementMode.Append, true) => "Data append successful",
            (DataManagementMode.Append, false) => $"Data append failed. Please refer to {ResolveLogFileName()} for more details.",
            (DataManagementMode.Overwrite, true) => "Data overwrite successful",
            (DataManagementMode.Overwrite, false) => $"Data overwrite failed. Please refer to {ResolveLogFileName()} for more details.",
            _ => result.ErrorMessage ?? "Data operation failed."
        };
        PageIndex = 3;
    }

    private static string ResolveLogFileName()
    {
        return FluxoLogManager.CurrentLogFileName;
    }

    private void EnableAllEntities()
    {
        _manifestAvailableEntities.Clear();
        _manifestAvailableEntities.UnionWith(Entities.Select(entity => entity.EntityKind));

        foreach (var entity in Entities)
        {
            entity.IsEnabled = true;
            entity.IsChecked = true;
        }

        ApplyDependencyRules();
    }

    private void ApplyDependencyRules()
    {
        var spendingSources = GetEntity(DataManagementEntityKind.SpendingSources);
        var canEnableDependents = _manifestAvailableEntities.Contains(DataManagementEntityKind.SpendingSources)
            && spendingSources.IsChecked;
        var dependentKinds = new[]
        {
            DataManagementEntityKind.Expenses,
            DataManagementEntityKind.Incomes,
            DataManagementEntityKind.RecurringTransactions
        };

        foreach (var dependentKind in dependentKinds)
        {
            var dependent = GetEntity(dependentKind);
            dependent.IsEnabled = _manifestAvailableEntities.Contains(dependentKind) && canEnableDependents;
            if (!dependent.IsEnabled)
                dependent.IsChecked = false;
        }
    }

    partial void OnModeChanged(DataManagementMode value)
    {
        OnPropertyChanged(nameof(IsBackupSelected));
        OnPropertyChanged(nameof(IsAppendSelected));
        OnPropertyChanged(nameof(IsOverwriteSelected));
        Conflicts.Clear();

        if (value == DataManagementMode.Backup)
            EnableAllEntities();
    }

    private void OnSpendingSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DataManagementEntityOptionVM.IsChecked))
            ApplyDependencyRules();
    }
}
