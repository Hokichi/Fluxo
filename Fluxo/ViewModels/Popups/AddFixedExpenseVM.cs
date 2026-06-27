using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.Logging;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.ViewModels.Shell;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;
using Fluxo.ViewModels.Popups.Helpers;

namespace Fluxo.ViewModels.Popups;

public partial class AddFixedExpenseVM : ObservableObject
{
    private const string DefaultTagColor = "#75B798";
    private const int NoAccountId = -1;
    private const int NoTagId = -1;

    private readonly MainVM _mainViewModel;
    private readonly IAppDataService _appData;
    private readonly Func<AddFixedExpenseInput, Task<AddFixedExpenseResult>>? _saveDraftAsync;
    private readonly Func<CancellationToken, Task<IReadOnlyList<TagVM>>>? _loadDraftTagsAsync;
    private readonly Func<string, string, Task<SettingsOperationResult>>? _createDraftTagAsync;
    private FormState _initialState;
    private bool _isChangeTrackingInitialized;
    private bool _isSyncingTagSelection;

    [ObservableProperty] private decimal _amountText;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _recurringTimeText = string.Empty;
    [ObservableProperty] private ExpenseCategory _selectedCategory = ExpenseCategory.Needs;
    [ObservableProperty] private AccountVM? _selectedAccount;
    [ObservableProperty] private TagVM? _selectedTag;
    [ObservableProperty] private TagOption? _selectedTagOption;
    [ObservableProperty] private string _tagNameText = "General";

    public int? EditingId { get; init; }

    public AddFixedExpenseVM(
        MainVM mainViewModel,
        IAppDataService appData,
        IReadOnlyList<AccountVM>? accountsOverride = null,
        int? forceIncludeAccountId = null,
        Func<AddFixedExpenseInput, Task<AddFixedExpenseResult>>? saveDraftAsync = null,
        Func<CancellationToken, Task<IReadOnlyList<TagVM>>>? loadDraftTagsAsync = null,
        Func<string, string, Task<SettingsOperationResult>>? createDraftTagAsync = null)
    {
        _mainViewModel = mainViewModel;
        _appData = appData;
        _saveDraftAsync = saveDraftAsync;
        _loadDraftTagsAsync = loadDraftTagsAsync;
        _createDraftTagAsync = createDraftTagAsync;
        AccountsView = AccountComboBoxViewFactory.CreateGroupedByTypeThenName(
            Accounts,
            nameof(AccountVM.TypeDisplayName),
            nameof(AccountVM.AccountType),
            nameof(AccountVM.Name));

        var sourceList = accountsOverride ??
                         _mainViewModel.BudgetPanel.Accounts
                             .ToList();

        sourceList = sourceList
            .Where(source => source.IsEnabled ||
                             (forceIncludeAccountId.HasValue && source.Id == forceIncludeAccountId.Value))
            .OrderBy(source => source.AccountType)
            .ThenBy(source => source.Name)
            .ToList();

        foreach (var account in sourceList)
            Accounts.Add(account);

        RecurringTimeText = MonthlyDueDateHelper.Normalize(DateTime.Today.Day)?.ToString(CultureInfo.InvariantCulture) ??
                            MonthlyDueDateHelper.MinMonthlyDay.ToString(CultureInfo.InvariantCulture);
        SelectedAccount = Accounts.FirstOrDefault();
        _initialState = CaptureState();
    }

    public ObservableCollection<AccountVM> Accounts { get; } = [];
    public ICollectionView AccountsView { get; }
    public ObservableCollection<TagVM> Tags { get; } = [];
    public ObservableCollection<TagOption> TagOptions { get; } = [];
    public bool IsEditMode => EditingId.HasValue;
    public string PopupTitle => IsEditMode ? "Edit Recurring Transaction" : "Add Recurring Transaction";
    public string HeaderTitle => IsEditMode ? "Edit Recurring Transaction" : "Add Recurring Transaction";

    public string HeaderDescription => IsEditMode
        ? "Update this recurring expense and save the changes."
        : "Add a recurring expense for rent, subscriptions, bills, and similar commitments.";

    public string ValidationDialogTitle => PopupTitle;

    public IReadOnlyList<ExpenseCategoryOption> Categories { get; } =
    [
        new("Needs", ExpenseCategory.Needs),
        new("Wants", ExpenseCategory.Wants),
        new("Invest", ExpenseCategory.Savings)
    ];

    public bool CanSave => !IsBusy && AreRequiredFieldsFilled();
    public bool HasChanges => _isChangeTrackingInitialized && !CaptureState().Equals(_initialState);
    public bool IsDraftMode => _saveDraftAsync is not null;
    public bool AllowAddTagAction => true;

    public void BeginChangeTracking()
    {
        _initialState = CaptureState();
        _isChangeTrackingInitialized = true;
        NotifyFormStateChanged();
    }

    partial void OnAmountTextChanged(decimal value) => NotifyFormStateChanged();

    partial void OnIsActiveChanged(bool value) => NotifyFormStateChanged();

    partial void OnIsBusyChanged(bool value) => NotifyFormStateChanged();

    partial void OnNameTextChanged(string value) => NotifyFormStateChanged();

    partial void OnRecurringTimeTextChanged(string value) => NotifyFormStateChanged();

    partial void OnSelectedCategoryChanged(ExpenseCategory value) => NotifyFormStateChanged();

    partial void OnSelectedAccountChanged(AccountVM? value) => NotifyFormStateChanged();

    partial void OnSelectedTagChanged(TagVM? value)
    {
        if (!_isSyncingTagSelection)
        {
            _isSyncingTagSelection = true;
            SelectedTagOption = value is null
                ? null
                : TagOptions.FirstOrDefault(option => !option.IsAddTagAction && option.Tag?.Id == value.Id);
            TagNameText = value?.Name ?? string.Empty;
            _isSyncingTagSelection = false;
        }

        NotifyFormStateChanged();
    }

    partial void OnSelectedTagOptionChanged(TagOption? value)
    {
        if (_isSyncingTagSelection || value?.IsAddTagAction != false || value.Tag is null)
            return;

        _isSyncingTagSelection = true;
        SelectedTag = value.Tag;
        _isSyncingTagSelection = false;
    }

    partial void OnTagNameTextChanged(string value)
    {
        if (!_isSyncingTagSelection)
        {
            _isSyncingTagSelection = true;
            SelectedTag = Tags.FirstOrDefault(tag =>
                string.Equals(tag.Name, value, StringComparison.OrdinalIgnoreCase));
            _isSyncingTagSelection = false;
        }

        NotifyFormStateChanged();
    }

    public async Task LoadTagsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TagVM> availableTags;
        if (_loadDraftTagsAsync is not null)
        {
            availableTags = await _loadDraftTagsAsync(cancellationToken);
        }
        else
        {
            availableTags = (await _appData.GetTagsAsync(cancellationToken))
                .Where(tag => !tag.IsSystemTag)
                .OrderBy(tag => tag.Name)
                .Select(tag => new TagVM
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    HexCode = tag.HexCode,
                    IsSystemTag = tag.IsSystemTag,
                    SpendingLimit = tag.SpendingLimit
                })
                .ToList();
        }

        Tags.Clear();
        foreach (var tag in availableTags.Where(c => !c.IsSystemTag))
            Tags.Add(tag);

        TagOptions.Clear();
        foreach (var tag in Tags)
            TagOptions.Add(new TagOption(tag, tag.Name, false));
        TagOptions.Add(new TagOption(null, "Add Tag", true));

        var preferredTagName = string.IsNullOrWhiteSpace(TagNameText) ? "General" : TagNameText.Trim();
        var matchedTag = Tags.FirstOrDefault(tag =>
            string.Equals(tag.Name, preferredTagName, StringComparison.OrdinalIgnoreCase));

        _isSyncingTagSelection = true;
        SelectedTag = matchedTag ?? Tags.FirstOrDefault();
        SelectedTagOption = SelectedTag is null
            ? null
            : TagOptions.FirstOrDefault(option => !option.IsAddTagAction && option.Tag?.Id == SelectedTag.Id);
        TagNameText = SelectedTag?.Name ?? string.Empty;
        _isSyncingTagSelection = false;

        NotifyFormStateChanged();
    }

    public async Task<SettingsOperationResult> CreateDraftTagAsync(string name, string hexCode, string spendingLimitText)
    {
        if (_createDraftTagAsync is null)
            return SettingsOperationResult.Failure("Unable to add a tag right now.");

        var result = await _createDraftTagAsync(name, hexCode);
        if (!result.IsSuccess)
            return result;

        var addedTagName = (name ?? string.Empty).Trim();
        await LoadTagsAsync();

        if (addedTagName.Length > 0)
        {
            var matchingTag = Tags.FirstOrDefault(tag =>
                string.Equals(tag.Name, addedTagName, StringComparison.OrdinalIgnoreCase));
            if (matchingTag is not null)
                SelectedTag = matchingTag;
        }

        return result;
    }

    public async Task<AddFixedExpenseResult> SaveAsync()
    {
        if (IsBusy)
            return AddFixedExpenseResult.Failure("A recurring transaction is already being saved.");

        if (!TryBuildInput(out var input, out var validationMessage))
            return AddFixedExpenseResult.Failure(validationMessage);

        if (_saveDraftAsync is not null)
            return await _saveDraftAsync(input);

        IsBusy = true;

        try
        {
            var account = await _appData.GetAccountByIdAsync(input.AccountId);
            if (account is null)
                return AddFixedExpenseResult.Failure("Please choose a valid account.");

            var existingTags = await _appData.GetTagsAsync();
            var tag = existingTags.FirstOrDefault(existing => existing.Id == input.TagId && !existing.IsSystemTag);

            if (tag is null)
            {
                tag = new Tag
                {
                    Name = input.TagName,
                    HexCode = DefaultTagColor
                };

                await _appData.AddTagAsync(tag);
                await _appData.SaveChangesAsync();
            }

            if (EditingId.HasValue)
            {
                var existing = await _appData.GetRecurringTransactionByIdAsync(EditingId.Value);
                if (existing is null)
                    return AddFixedExpenseResult.Failure("Recurring transaction not found.");

                existing.Name = input.Name;
                existing.Amount = input.Amount;
                existing.Category = input.Category;
                existing.SourceId = account.Id;
                existing.TagId = tag.Id;
                existing.RecurringPeriod = RecurringPeriod.Monthly;
                existing.RecurringTime = input.RecurringTime;
                existing.IsEnabled = input.IsActive;
                _appData.UpdateRecurringTransaction(existing);
            }
            else
            {
                var expense = new RecurringTransaction
                {
                    Name = input.Name,
                    Amount = input.Amount,
                    Type = RecurringTransactionType.Expense,
                    Category = input.Category,
                    SourceId = account.Id,
                    TagId = tag.Id,
                    RecurringPeriod = RecurringPeriod.Monthly,
                    RecurringTime = input.RecurringTime,
                    IsEnabled = input.IsActive
                };
                await _appData.AddRecurringTransactionAsync(expense);
            }

            await _appData.SaveChangesAsync();
            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));

            return AddFixedExpenseResult.Success(true);
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to create this recurring transaction.");
            return AddFixedExpenseResult.Failure(FluxoLogManager.CreateFailureMessage("create recurring transaction"));
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

        if (AmountText <= 0m)
        {
            validationMessage = "Please enter a valid amount greater than zero.";
            return false;
        }

        if (SelectedAccount is null)
        {
            validationMessage = "Please select a account.";
            return false;
        }

        if (SelectedTag is null)
        {
            validationMessage = "Please select a tag.";
            return false;
        }

        var tagName = (SelectedTag.Name ?? string.Empty).Trim();
        if (tagName.Length == 0)
        {
            validationMessage = "Please select a tag.";
            return false;
        }

        if (!TryParseRecurringTime(RecurringTimeText, out var recurringTime))
        {
            validationMessage = "Recurring time must be a number between 1 and 28.";
            return false;
        }

        input = new AddFixedExpenseInput(
            name,
            AmountText,
            SelectedCategory,
            SelectedAccount.Id,
            recurringTime,
            SelectedTag.Id,
            tagName,
            IsActive);

        return true;
    }

    private static bool TryParseRecurringTime(string text, out int recurringTime)
    {
        recurringTime = 0;
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out recurringTime) &&
               recurringTime is >= MonthlyDueDateHelper.MinMonthlyDay and <= MonthlyDueDateHelper.MaxMonthlyDay;
    }

    private bool AreRequiredFieldsFilled()
    {
        return !string.IsNullOrWhiteSpace(NameText) &&
               AmountText > 0m &&
               TryParseRecurringTime(RecurringTimeText, out _) &&
               SelectedTag is not null &&
               SelectedAccount is not null;
    }

    private FormState CaptureState()
    {
        return new FormState(
            NameText ?? string.Empty,
            AmountText,
            SelectedCategory,
            SelectedAccount?.Id ?? NoAccountId,
            RecurringTimeText ?? string.Empty,
            SelectedTag?.Id ?? NoTagId,
            IsActive);
    }

    private void NotifyFormStateChanged()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(HasChanges));
    }

    public sealed record ExpenseCategoryOption(string Label, ExpenseCategory Value);
    public sealed record TagOption(TagVM? Tag, string Label, bool IsAddTagAction);

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

    public readonly record struct AddFixedExpenseInput(
        string Name,
        decimal Amount,
        ExpenseCategory Category,
        int AccountId,
        int RecurringTime,
        int TagId,
        string TagName,
        bool IsActive);

    private readonly record struct FormState(
        string NameText,
        decimal AmountText,
        ExpenseCategory SelectedCategory,
        int SelectedAccountId,
        string RecurringTimeText,
        int SelectedTagId,
        bool IsActive);
}
