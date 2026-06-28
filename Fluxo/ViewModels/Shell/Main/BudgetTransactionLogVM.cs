using Fluxo.ViewModels.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Shell.Main;

public sealed class BudgetTransactionLogVM
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required decimal Amount { get; init; }

    public required string AmountText { get; init; }

    public required DateTime OccurredOn { get; init; }
    public required DateTime LoggedOn { get; init; }

    public required AccountVM Account { get; init; }

    public string? TagHexCode { get; init; }

    public required TransactionVM Transaction { get; init; }

    public bool IsExpense => Transaction.Type == TransactionType.Expense;
}
