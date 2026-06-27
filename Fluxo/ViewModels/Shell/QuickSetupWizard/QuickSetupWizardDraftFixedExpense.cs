using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public sealed record QuickSetupWizardDraftFixedExpense(
    int Id,
    string Name,
    decimal Amount,
    ExpenseCategory Category,
    int AccountId,
    RecurringPeriod RecurringPeriod,
    int RecurringTime,
    int TagId,
    string TagName,
    bool IsActive);
