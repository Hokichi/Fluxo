using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Popups;

public sealed partial class AddNewTransactionHistoryItemVM : ObservableObject
{
    public int Id { get; init; }
    public bool IsExpense { get; init; }
    public bool IsGoalUpdate { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public ExpenseCategory? Category { get; init; }
    public int? TagId { get; init; }
    public string? TagHexCode { get; init; }
    public bool IsPinned { get; init; }
}
