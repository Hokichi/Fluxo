using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.ViewModels.Helpers;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;

namespace Fluxo.ViewModels.Popups;

public partial class AddFixedExpenseVM : ObservableObject
{
    private const string DefaultTagColor = "#75B798";
    private const int NoSpendingSourceId = -1;

    private readonly MainVM _mainViewModel;
    private readonly IUnitOfWork _unitOfWork;
    private FormState _initialState;
    private bool _isChangeTrackingInitialized;

    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _recurringDateText = string.Empty;
    [ObservableProperty] private ExpenseCategory _selectedCategory = ExpenseCategory.Needs;
    [ObservableProperty] private SpendingSourceVM? _selectedSpendingSource;
    [ObservableProperty] private string _tagNameText = "General";

    public int? EditingId { get; init; }

    public AddFixedExpenseVM(MainVM mainViewModel, IUnitOfWork unitOfWork)
    {
        _mainViewModel = mainViewModel;
        _unitOfWork = unitOfWork;

        foreach (var spendingSource in _mainViewModel.BudgetPanel.SpendingSources
                     .Where(source => source.IsEnabled)
                     .OrderBy(source => source.Name))
            SpendingSources.Add(spendingSource);

        RecurringDateText = MonthlyDueDateHelper.Normalize(DateTime.Today.Day)?.ToString(CultureInfo.InvariantCulture) ??
                            MonthlyDueDateHelper.MinMonthlyDay.ToString(CultureInfo.InvariantCulture);
        SelectedSpendingSource = SpendingSources.FirstOrDefault();
        _initialState = CaptureState();
    }

    public ObservableCollection<SpendingSourceVM> SpendingSources { get; } = [];

    public IReadOnlyList<ExpenseCategoryOption> Categories { get; } =
    [
        new("Needs", ExpenseCategory.Needs),
        new("Wants", ExpenseCategory.Wants),
        new("Invest", ExpenseCategory.Savings)
    ];

    public bool CanSave => !IsBusy && AreRequiredFieldsFilled();
    public bool HasChanges => _isChangeTrackingInitialized && !CaptureState().Equals(_initialState);

    public void BeginChangeTracking()
    {
        _initialState = CaptureState();
        _isChangeTrackingInitialized = true;
        NotifyFormStateChanged();
    }

    partial void OnAmountTextChanged(string value) => NotifyFormStateChanged();
    partial void OnIsActiveChanged(bool value) => NotifyFormStateChanged();
    partial void OnIsBusyChanged(bool value) => NotifyFormStateChanged();
    partial void OnNameTextChanged(string value) => NotifyFormStateChanged();
    partial void OnRecurringDateTextChanged(string value) => NotifyFormStateChanged();
    partial void OnSelectedCategoryChanged(ExpenseCategory value) => NotifyFormStateChanged();
    partial void OnSelectedSpendingSourceChanged(SpendingSourceVM? value) => NotifyFormStateChanged();
    partial void OnTagNameTextChanged(string value) => NotifyFormStateChanged();

    public async Task<AddFixedExpenseResult> SaveAsync()
    {
        if (IsBusy)
            return AddFixedExpenseResult.Failure("A fixed expense is already being saved.");

        if (!TryBuildInput(out var input, out var validationMessage))
            return AddFixedExpenseResult.Failure(validationMessage);

        IsBusy = true;

        try
        {
            var unitOfWork = _unitOfWork;

            var spendingSource = await unitOfWork.SpendingSources.GetByIdAsync(input.SpendingSourceId);
            if (spendingSource is null)
                return AddFixedExpenseResult.Failure("Please choose a valid spending source.");

            var existingTags = await unitOfWork.ExpenseTags.GetAllAsync();
            var tag = existingTags.FirstOrDefault(existing =>
                string.Equals(existing.Name, input.TagName, StringComparison.OrdinalIgnoreCase));

            if (tag is null)
            {
                tag = new ExpenseTag
                {
                    Name = input.TagName,
                    HexCode = DefaultTagColor,
                    IconName = string.Empty
                };

                await unitOfWork.ExpenseTags.AddAsync(tag);
                await unitOfWork.SaveChangesAsync();
            }

            if (EditingId.HasValue)
            {
                var existing = await unitOfWork.Expenses.GetByIdAsync(EditingId.Value);
                if (existing is null)
                    return AddFixedExpenseResult.Failure("Fixed expense not found.");

                existing.Name = input.Name;
                existing.Amount = input.Amount;
                existing.ExpenseCategory = input.Category;
                existing.RecurringDate = input.RecurringDate;
                existing.SpendingSourceId = spendingSource.Id;
                existing.ExpenseTagId = tag.Id;
                existing.IsActive = input.IsActive;
                unitOfWork.Expenses.Update(existing);
            }
            else
            {
                var expense = new Expense
                {
                    Name = input.Name,
                    Amount = input.Amount,
                    ExpenseKind = ExpenseKind.Fixed,
                    ExpenseCategory = input.Category,
                    RecurringDate = input.RecurringDate,
                    SpendingSourceId = spendingSource.Id,
                    ExpenseTagId = tag.Id,
                    IsActive = input.IsActive
                };
                await unitOfWork.Expenses.AddAsync(expense);
            }

            await unitOfWork.SaveChangesAsync();
            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));

            return AddFixedExpenseResult.Success(true);
        }
        catch (Exception exception)
        {
            return AddFixedExpenseResult.Failure($"Unable to create this fixed expense.\n\n{exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryBuildInput(out AddFixedExpenseInput input, out string validationMessage)
    {
        input = default;
        validationMessage = string.Empty;

        var name = (NameText ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            validationMessage = "Please enter an expense name.";
            return false;
        }

        if (!TryParseDecimal(AmountText, out var amount) || amount <= 0m)
        {
            validationMessage = "Please enter a valid amount greater than zero.";
            return false;
        }

        if (SelectedSpendingSource is null)
        {
            validationMessage = "Please select a spending source.";
            return false;
        }

        var tagName = string.IsNullOrWhiteSpace(TagNameText) ? "General" : TagNameText.Trim();
        if (!TryParseRecurringDate(RecurringDateText, out var recurringDate))
        {
            validationMessage = "Recurring date must be a number between 1 and 28.";
            return false;
        }

        input = new AddFixedExpenseInput(
            name,
            amount,
            SelectedCategory,
            SelectedSpendingSource.Id,
            recurringDate,
            tagName,
            IsActive);

        return true;
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        value = 0m;
        var normalizedText = (text ?? string.Empty)
            .Trim()
            .Replace(CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol, string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(normalizedText))
            return false;

        return decimal.TryParse(normalizedText, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                   CultureInfo.CurrentCulture, out value) ||
               decimal.TryParse(normalizedText, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                   CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseRecurringDate(string text, out int recurringDate)
    {
        recurringDate = 0;
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out recurringDate) &&
               recurringDate is >= MonthlyDueDateHelper.MinMonthlyDay and <= MonthlyDueDateHelper.MaxMonthlyDay;
    }

    private bool AreRequiredFieldsFilled()
    {
        return !string.IsNullOrWhiteSpace(NameText) &&
               !string.IsNullOrWhiteSpace(AmountText) &&
               TryParseRecurringDate(RecurringDateText, out _) &&
               !string.IsNullOrWhiteSpace(TagNameText) &&
               SelectedSpendingSource is not null;
    }

    private FormState CaptureState()
    {
        return new FormState(
            NameText ?? string.Empty,
            AmountText ?? string.Empty,
            SelectedCategory,
            SelectedSpendingSource?.Id ?? NoSpendingSourceId,
            RecurringDateText ?? string.Empty,
            TagNameText ?? string.Empty,
            IsActive);
    }

    private void NotifyFormStateChanged()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(HasChanges));
    }

    public sealed record ExpenseCategoryOption(string Label, ExpenseCategory Value);

    public readonly record struct AddFixedExpenseResult(bool IsSuccess, bool ShouldClose, string? ErrorMessage)
    {
        public static AddFixedExpenseResult Success(bool shouldClose = false)
        {
            return new AddFixedExpenseResult(true, shouldClose, null);
        }

        public static AddFixedExpenseResult Failure(string? errorMessage)
        {
            return new AddFixedExpenseResult(false, false, errorMessage);
        }
    }

    private readonly record struct AddFixedExpenseInput(
        string Name,
        decimal Amount,
        ExpenseCategory Category,
        int SpendingSourceId,
        int RecurringDate,
        string TagName,
        bool IsActive);

    private readonly record struct FormState(
        string NameText,
        string AmountText,
        ExpenseCategory SelectedCategory,
        int SelectedSpendingSourceId,
        string RecurringDateText,
        string TagNameText,
        bool IsActive);
}

