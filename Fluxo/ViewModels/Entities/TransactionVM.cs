using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Entities;

public partial class TransactionVM : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private TransactionType _type;
    [ObservableProperty] private int _sourceAccountId;
    [ObservableProperty] private int? _goalId;
    [ObservableProperty] private int? _repaymentAccountId;
    [ObservableProperty] private AccountVM _account = new();
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private DateTime _occurredOn;
    [ObservableProperty] private DateTime _loggedOn;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private ExpenseCategory? _expenseCategory;
    [ObservableProperty] private TagVM? _tag;
    [ObservableProperty] private int? _parentTransactionId;
    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _isForDeletion;
    [ObservableProperty] private bool _isIoU;
    [ObservableProperty] private bool _isExcludedFromBudget;
}
