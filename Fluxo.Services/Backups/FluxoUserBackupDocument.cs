using Fluxo.Core.Enums;
using System.Text.Json.Serialization;

namespace Fluxo.Services.Backups;

public sealed class FluxoUserBackupDocument
{
    public int SchemaVersion { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public List<DataManagementEntityKind> IncludedEntities { get; set; } = [];
    public FluxoUserBackupEntities Entities { get; set; } = new();
}

public sealed class FluxoUserBackupEntities
{
    public List<BackupAccount> Accounts { get; set; } = [];
    public List<BackupExpenseTag> Tags { get; set; } = [];
    public List<BackupSavingGoal> Goals { get; set; } = [];
    public List<BackupExpense> Expenses { get; set; } = [];
    public List<BackupExpenseLog> ExpenseLogs { get; set; } = [];
    public List<BackupIncomeLog> IncomeLogs { get; set; } = [];
    public List<BackupRecurringTransaction> RecurringTransactions { get; set; } = [];
    public List<BackupUserSetting> UserSettings { get; set; } = [];
}

public sealed record BackupAccount(
    int BackupId,
    string Name,
    string AccountType,
    decimal AccountLimit,
    decimal MaximumSpending,
    decimal? MinimumPayment,
    decimal SpentAmount,
    decimal Balance,
    int? MonthlyDueDate,
    int? DeductSourceBackupId,
    decimal? InterestRate,
    bool PinnedOnUI,
    bool IsEnabled,
    bool IsForDeletion)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("showOnUI")]
    public bool? LegacyShowOnUI { get; init; }

    [JsonIgnore]
    public bool RestoredPinnedOnUI => LegacyShowOnUI ?? PinnedOnUI;
}

public sealed record BackupExpenseTag(
    int BackupId,
    string Name,
    string HexCode,
    bool IsSystemTag,
    decimal? SpendingLimit = null);

public sealed record BackupSavingGoal(
    int BackupId,
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    DateTime? SavingEndDate,
    DateTime CreatedOn);

public sealed record BackupExpense(
    int BackupId,
    int AccountBackupId,
    int ExpenseTagBackupId,
    string Name,
    decimal Amount,
    string ExpenseCategory);

public sealed record BackupExpenseLog(
    int BackupId,
    int ExpenseBackupId,
    int AccountBackupId,
    decimal Amount,
    DateTime DeductedOn,
    string Notes,
    bool IsForDeletion,
    bool IsPinned = false,
    int? ParentLogBackupId = null);

public sealed record BackupIncomeLog(
    int BackupId,
    int AccountBackupId,
    string Name,
    decimal Amount,
    DateTime AddedOn,
    string Notes,
    bool IsPinned = false);

public sealed record BackupRecurringTransaction(
    int BackupId,
    string Name,
    decimal Amount,
    string RecurringPeriod,
    int RecurringTime,
    string Type,
    int SourceBackupId,
    int? TagBackupId,
    int? GoalBackupId,
    bool IsEnabled);

public sealed record BackupUserSetting(string Name, string Value);
