using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Popups;

public partial class TransactionSplitRowVM : ObservableObject
{
    [ObservableProperty] private decimal _amountText;
    [ObservableProperty] private int? _transactionId;
    [ObservableProperty] private bool _isCausingNegativeRemainder;
    [ObservableProperty] private bool _isIoU;
    [ObservableProperty] private bool _isTagPopupOpen;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private ExpenseCategory _selectedExpenseCategory = ExpenseCategory.Needs;
    [ObservableProperty] private TagVM? _selectedTag;

    public bool HasAmount => AmountText > 0m;
    public bool CanToggleRecurring => true;
    public bool CanUseIoU => true;

    public bool IsRegularMode
    {
        get => !IsIoU;
        set { if (value) IsIoU = false; }
    }

    public bool IsPostedIoUMode
    {
        get => IsIoU;
        set { if (value) IsIoU = true; }
    }

    public bool HasMeaningfulValue =>
        AmountText > 0m ||
        IsIoU ||
        !string.IsNullOrWhiteSpace(NameText) ||
        SelectedTag is not null;

    public string AmountValidationHint =>
        IsCausingNegativeRemainder ? "Amount exceeds parent expense." : string.Empty;

    public string TagDisplayName => SelectedTag?.Name ?? "Tag";

    partial void OnAmountTextChanged(decimal value)
    {
        OnPropertyChanged(nameof(HasAmount));
        OnPropertyChanged(nameof(HasMeaningfulValue));
    }

    partial void OnNameTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasMeaningfulValue));
    }

    partial void OnIsIoUChanged(bool value)
    {
        OnPropertyChanged(nameof(HasMeaningfulValue));
        OnPropertyChanged(nameof(IsRegularMode));
        OnPropertyChanged(nameof(IsPostedIoUMode));
    }

    partial void OnSelectedTagChanged(TagVM? value)
    {
        OnPropertyChanged(nameof(HasMeaningfulValue));
        OnPropertyChanged(nameof(TagDisplayName));
        IsTagPopupOpen = false;
    }

    partial void OnIsCausingNegativeRemainderChanged(bool value)
    {
        OnPropertyChanged(nameof(AmountValidationHint));
    }
}
