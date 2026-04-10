using CommunityToolkit.Mvvm.Messaging.Messages;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Messages;

[Flags]
public enum ExpenseDetailChangedFields
{
    None = 0,
    Name = 1 << 0,
    Amount = 1 << 1,
    Date = 1 << 2,
    Category = 1 << 3,
    SpendingSource = 1 << 4,
    Tag = 1 << 5,
    Note = 1 << 6
}

public sealed class ExpenseDetailUpdatedMessage(ExpenseDetailUpdate value)
    : ValueChangedMessage<ExpenseDetailUpdate>(value);

public sealed record ExpenseDetailUpdate(
    int ExpenseLogId,
    ExpenseDetailSnapshot PreviousState,
    ExpenseDetailChangedFields ChangedFields)
{
    public bool HasChanges => ChangedFields != ExpenseDetailChangedFields.None;

    public bool AffectsAllTimeTotals =>
        (ChangedFields & (ExpenseDetailChangedFields.Amount | ExpenseDetailChangedFields.Category)) != 0;

    public bool AffectsTagOrdering => (ChangedFields & ExpenseDetailChangedFields.Tag) != 0;

    public bool AffectsVisibleMoneyOut =>
        (ChangedFields &
         (ExpenseDetailChangedFields.Amount | ExpenseDetailChangedFields.Date | ExpenseDetailChangedFields.SpendingSource)) != 0;

    public bool AffectsSpendingSourceState =>
        (ChangedFields & (ExpenseDetailChangedFields.Amount | ExpenseDetailChangedFields.SpendingSource)) != 0;
}

public sealed record ExpenseDetailSnapshot(
    decimal Amount,
    DateTime Date,
    ExpenseCategory Category,
    int SpendingSourceId,
    int TagId);
