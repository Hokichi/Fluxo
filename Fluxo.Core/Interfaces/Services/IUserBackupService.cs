using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Services;

public interface IUserBackupService
{
    string GetDefaultBackupDirectory();
    string BuildDefaultBackupPath(DateTime timestamp);
    Task<UserBackupManifest> ReadManifestAsync(string filePath, CancellationToken cancellationToken = default);
    Task<UserBackupOperationResult> BackupAsync(UserBackupSelection selection, string filePath,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserBackupConflict>> FindAppendConflictsAsync(string filePath, UserBackupSelection selection,
        CancellationToken cancellationToken = default);
    Task<UserBackupOperationResult> AppendAsync(string filePath, UserBackupSelection selection,
        IReadOnlyDictionary<string, DataManagementConflictDecision> conflictDecisions,
        CancellationToken cancellationToken = default);
    Task<UserBackupOperationResult> OverwriteAsync(string filePath, UserBackupSelection selection,
        CancellationToken cancellationToken = default);
}

public sealed record UserBackupSelection(IReadOnlySet<DataManagementEntityKind> Entities)
{
    public bool Includes(DataManagementEntityKind entity) => Entities.Contains(entity);
}

public sealed record UserBackupManifest(
    int SchemaVersion,
    DateTime CreatedAt,
    IReadOnlySet<DataManagementEntityKind> IncludedEntities);

public sealed record UserBackupConflict(
    string ConflictKey,
    DataManagementEntityKind EntityKind,
    string Name);

public sealed record UserBackupOperationResult(
    bool IsSuccess,
    string? ErrorMessage,
    string? SafetyBackupPath)
{
    public static UserBackupOperationResult Success(string? safetyBackupPath = null) =>
        new(true, null, safetyBackupPath);

    public static UserBackupOperationResult Failure(string errorMessage, string? safetyBackupPath = null) =>
        new(false, errorMessage, safetyBackupPath);
}

