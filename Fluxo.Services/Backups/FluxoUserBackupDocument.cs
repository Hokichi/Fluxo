using Fluxo.Core.Enums;

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
    public List<BackupSpendingSource> SpendingSources { get; set; } = [];
    public List<BackupExpenseTag> Tags { get; set; } = [];
    public List<BackupSavingGoal> Goals { get; set; } = [];
    public List<BackupExpense> Expenses { get; set; } = [];
    public List<BackupExpenseLog> ExpenseLogs { get; set; } = [];
    public List<BackupIncomeLog> IncomeLogs { get; set; } = [];
    public List<BackupRecurringTransaction> RecurringTransactions { get; set; } = [];
    public List<BackupUserSetting> UserSettings { get; set; } = [];
}

public sealed record BackupSpendingSource(
    int BackupId,
    string Name,
    string SpendingSourceType,
    decimal AccountLimit,
    decimal MaximumSpending,
    decimal? MinimumPayment,
    decimal SpentAmount,
    decimal Balance,
    int? MonthlyDueDate,
    int? DeductSourceBackupId,
    decimal? InterestRate,
    bool ShowOnUI,
    bool IsEnabled,
    bool IsForDeletion);

public sealed record BackupExpenseTag(int BackupId, string Name, string HexCode, bool IsSystemTag);

public sealed record BackupSavingGoal(
    int BackupId,
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    DateTime? SavingEndDate,
    DateTime CreatedOn);

public sealed record BackupExpense(
    int BackupId,
    int SpendingSourceBackupId,
    int ExpenseTagBackupId,
    string Name,
    decimal Amount,
    string ExpenseCategory);

public sealed record BackupExpenseLog(
    int BackupId,
    int ExpenseBackupId,
    int SpendingSourceBackupId,
    decimal Amount,
    DateTime DeductedOn,
    string Notes,
    bool IsForDeletion);

public sealed record BackupIncomeLog(
    int BackupId,
    int SpendingSourceBackupId,
    string Name,
    decimal Amount,
    DateTime AddedOn,
    string Notes);

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

