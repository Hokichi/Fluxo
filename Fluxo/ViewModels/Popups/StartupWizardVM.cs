using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.ViewModels.Shell;

namespace Fluxo.ViewModels.Popups;

public partial class StartupWizardVM : ObservableObject
{
    private const string DefaultCurrencyCode = "USD";

    private readonly MainVM _mainViewModel;
    private readonly Func<IUnitOfWork> _unitOfWorkFactory;

    [ObservableProperty] private int _currentStepIndex;
    [ObservableProperty] private int _investAllocationPercentage = 20;
    [ObservableProperty] private int _needsAllocationPercentage = 50;
    [ObservableProperty] private string _salaryText = string.Empty;
    [ObservableProperty] private string _selectedCurrencyCode = DefaultCurrencyCode;
    [ObservableProperty] private string _usernameText = "User";
    [ObservableProperty] private int _wantsAllocationPercentage = 30;

    public StartupWizardVM(MainVM mainViewModel, Func<IUnitOfWork> unitOfWorkFactory)
    {
        _mainViewModel = mainViewModel;
        _unitOfWorkFactory = unitOfWorkFactory;

        foreach (var option in BuildCurrencyOptions())
            CurrencyOptions.Add(option);
    }

    public ObservableCollection<StartupWizardSpendingSourceItemVM> SpendingSources { get; } = [];
    public ObservableCollection<StartupWizardFixedExpenseItemVM> FixedExpenses { get; } = [];
    public ObservableCollection<StartupWizardSavingGoalItemVM> SavingGoals { get; } = [];
    public ObservableCollection<SettingsNotificationOptionVM> NotificationSettings { get; } = [];
    public ObservableCollection<SettingsCurrencyOptionVM> CurrencyOptions { get; } = [];

    public int TotalSteps => 7;
    public bool IsFirstStep => CurrentStepIndex == 0;
    public bool IsFinalStep => CurrentStepIndex == TotalSteps - 1;
    public bool IsStep1Active => CurrentStepIndex == 0;
    public bool IsStep2Active => CurrentStepIndex == 1;
    public bool IsStep3Active => CurrentStepIndex == 2;
    public bool IsStep4Active => CurrentStepIndex == 3;
    public bool IsStep5Active => CurrentStepIndex == 4;
    public bool IsStep6Active => CurrentStepIndex == 5;
    public bool IsStep7Active => CurrentStepIndex == 6;
    public string StepCounterText => $"Step {CurrentStepIndex + 1} of {TotalSteps}";
    public decimal TotalBudgetAmount => ParseSalaryAmount();
    public string SelectedCurrencySymbol =>
        CurrencyOptions.FirstOrDefault(option =>
            string.Equals(option.Code, SelectedCurrencyCode, StringComparison.OrdinalIgnoreCase))?.Symbol ?? "$";
    public string NeedsAllocationAmountText => BuildAllocationAmountText(NeedsAllocationPercentage);
    public string WantsAllocationAmountText => BuildAllocationAmountText(WantsAllocationPercentage);
    public string InvestAllocationAmountText => BuildAllocationAmountText(InvestAllocationPercentage);
    public string ResolvedUsername => string.IsNullOrWhiteSpace(UsernameText) ? "User" : UsernameText.Trim();

    public string CurrentStepTitle => CurrentStepIndex switch
    {
        0 => "Tell us about you",
        1 => "Add spending sources",
        2 => "Add fixed expenses",
        3 => "Add savings goals",
        4 => "Budget allocation",
        5 => "Notification preferences",
        _ => "Welcome to Fluxo"
    };

    public string CurrentStepDescription => CurrentStepIndex switch
    {
        0 => "Set your display name and optionally add a salary baseline.",
        1 => "Add the accounts and sources you spend from most often.",
        2 => "Add recurring fixed expenses so Fluxo can account for them upfront.",
        3 => "Add a few goals to start tracking progress right away.",
        4 => "Split your salary into Needs, Wants, and Invest.",
        5 => "Choose which reminders and alerts Fluxo should show.",
        _ => $"You're ready, {ResolvedUsername}. Fluxo is all set to open."
    };

    partial void OnCurrentStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsFirstStep));
        OnPropertyChanged(nameof(IsFinalStep));
        OnPropertyChanged(nameof(IsStep1Active));
        OnPropertyChanged(nameof(IsStep2Active));
        OnPropertyChanged(nameof(IsStep3Active));
        OnPropertyChanged(nameof(IsStep4Active));
        OnPropertyChanged(nameof(IsStep5Active));
        OnPropertyChanged(nameof(IsStep6Active));
        OnPropertyChanged(nameof(IsStep7Active));
        OnPropertyChanged(nameof(StepCounterText));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(CurrentStepDescription));
    }

    partial void OnSelectedCurrencyCodeChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedCurrencySymbol));
        OnPropertyChanged(nameof(NeedsAllocationAmountText));
        OnPropertyChanged(nameof(WantsAllocationAmountText));
        OnPropertyChanged(nameof(InvestAllocationAmountText));
    }

    partial void OnNeedsAllocationPercentageChanged(int value)
    {
        OnPropertyChanged(nameof(NeedsAllocationAmountText));
    }

    partial void OnWantsAllocationPercentageChanged(int value)
    {
        OnPropertyChanged(nameof(WantsAllocationAmountText));
    }

    partial void OnInvestAllocationPercentageChanged(int value)
    {
        OnPropertyChanged(nameof(InvestAllocationAmountText));
    }

    partial void OnSalaryTextChanged(string value)
    {
        OnPropertyChanged(nameof(NeedsAllocationAmountText));
        OnPropertyChanged(nameof(WantsAllocationAmountText));
        OnPropertyChanged(nameof(InvestAllocationAmountText));
    }

    partial void OnUsernameTextChanged(string value)
    {
        OnPropertyChanged(nameof(ResolvedUsername));
        OnPropertyChanged(nameof(CurrentStepDescription));
    }

    public async Task LoadAsync()
    {
        await LoadSettingsAsync();
        await RefreshCollectionsAsync();
    }

    public void GoBack()
    {
        if (CurrentStepIndex > 0)
            CurrentStepIndex--;
    }

    public async Task<SettingsOperationResult> GoNextAsync()
    {
        var result = await PersistCurrentStepAsync();
        if (!result.IsSuccess)
            return result;

        if (CurrentStepIndex < TotalSteps - 1)
            CurrentStepIndex++;

        return SettingsOperationResult.Success();
    }

    public async Task<SettingsOperationResult> CompleteAsync()
    {
        var result = await PersistAllAsync();
        if (!result.IsSuccess)
            return result;

        await SaveIsFirstRunAsync(false);
        await _mainViewModel.ReloadCurrentDataAsync(true);
        return SettingsOperationResult.Success();
    }

    public async Task<SettingsOperationResult> DismissAsync()
    {
        var result = await PersistCurrentStepAsync();
        if (!result.IsSuccess)
            return result;

        await SaveIsFirstRunAsync(false);
        await _mainViewModel.ReloadCurrentDataAsync(true);
        return SettingsOperationResult.Success();
    }

    public AddSpendingSourceVM CreateAddSpendingSourceViewModel()
    {
        return new AddSpendingSourceVM(_mainViewModel, _unitOfWorkFactory);
    }

    public AddFixedExpenseVM CreateAddFixedExpenseViewModel()
    {
        return new AddFixedExpenseVM(_mainViewModel, _unitOfWorkFactory);
    }

    public AddSavingGoalVM CreateAddSavingGoalViewModel()
    {
        return new AddSavingGoalVM(_mainViewModel, _unitOfWorkFactory);
    }

    public async Task RefreshCollectionsAsync()
    {
        await RefreshSpendingSourcesAsync();
        await RefreshFixedExpensesAsync();
        await RefreshSavingGoalsAsync();
    }

    public async Task RefreshSpendingSourcesAsync()
    {
        await using var unitOfWork = _unitOfWorkFactory();
        ReplaceCollection(SpendingSources, (await unitOfWork.SpendingSources.GetAllAsync())
            .OrderBy(source => source.Name)
            .Select(source => new StartupWizardSpendingSourceItemVM(source)));
    }

    public async Task RefreshFixedExpensesAsync()
    {
        await using var unitOfWork = _unitOfWorkFactory();
        ReplaceCollection(FixedExpenses, (await unitOfWork.Expenses.GetByKindAsync(ExpenseKind.Fixed))
            .OrderBy(expense => expense.Name)
            .Select(expense => new StartupWizardFixedExpenseItemVM(expense)));
    }

    public async Task RefreshSavingGoalsAsync()
    {
        await using var unitOfWork = _unitOfWorkFactory();
        ReplaceCollection(SavingGoals, (await unitOfWork.SavingGoals.GetAllAsync())
            .OrderBy(goal => goal.SavingEndDate)
            .ThenBy(goal => goal.Name)
            .Select(goal => new StartupWizardSavingGoalItemVM(goal)));
    }

    private async Task LoadSettingsAsync()
    {
        await using var unitOfWork = _unitOfWorkFactory();
        var settings = await unitOfWork.UserSettings.GetAllAsync();
        var settingsByName = settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);

        UsernameText = ParseString(settingsByName, UserSettingNames.PreferredDisplayName, "User");
        SalaryText = ParseString(settingsByName, UserSettingNames.Salary, string.Empty);
        SelectedCurrencyCode = ParseCurrencyCode(settingsByName, UserSettingNames.PreferredCurrencyCode, DefaultCurrencyCode);
        NeedsAllocationPercentage = ParsePercentage(settingsByName, UserSettingNames.NeedsThreshold, 50m);
        WantsAllocationPercentage = ParsePercentage(settingsByName, UserSettingNames.WantsThreshold, 30m);
        InvestAllocationPercentage = ParsePercentage(settingsByName, UserSettingNames.InvestThreshold, 20m);

        ReplaceCollection(NotificationSettings,
        [
            new SettingsNotificationOptionVM(
                "Upcoming fixed expense reminders",
                "Warn before recurring fixed expenses are due.",
                UserSettingNames.IsFixedExpensesDeductionNotifEnabled,
                ParseBool(settingsByName, UserSettingNames.IsFixedExpensesDeductionNotifEnabled, false)),
            new SettingsNotificationOptionVM(
                "Credit deadline reminders",
                "Warn when credit and BNPL due dates are approaching.",
                UserSettingNames.IsCreditDeadlineNotifEnabled,
                ParseBool(settingsByName, UserSettingNames.IsCreditDeadlineNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Budget threshold alerts",
                "Warn when Needs, Wants, or Invest allocations are nearly spent.",
                UserSettingNames.IsBudgetThresholdNotifEnabled,
                ParseBool(settingsByName, UserSettingNames.IsBudgetThresholdNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Low credit usage alerts",
                "Warn when credit or BNPL sources cross their usage threshold.",
                UserSettingNames.IsLowCreditNotifEnabled,
                ParseBool(settingsByName, UserSettingNames.IsLowCreditNotifEnabled, false)),
            new SettingsNotificationOptionVM(
                "Low account balance alerts",
                "Warn when checking or cash sources are running low.",
                UserSettingNames.IsLowAccountBalanceNotifEnabled,
                ParseBool(settingsByName, UserSettingNames.IsLowAccountBalanceNotifEnabled, false))
        ]);
    }

    private async Task<SettingsOperationResult> PersistCurrentStepAsync()
    {
        return CurrentStepIndex switch
        {
            0 => await SavePersonalizationAsync(),
            4 => await SaveBudgetAllocationAsync(),
            5 => await SaveNotificationsAsync(),
            _ => SettingsOperationResult.Success()
        };
    }

    private async Task<SettingsOperationResult> PersistAllAsync()
    {
        var personal = await SavePersonalizationAsync();
        if (!personal.IsSuccess)
            return personal;

        var budget = await SaveBudgetAllocationAsync();
        if (!budget.IsSuccess)
            return budget;

        return await SaveNotificationsAsync();
    }

    private async Task<SettingsOperationResult> SavePersonalizationAsync()
    {
        var username = string.IsNullOrWhiteSpace(UsernameText) ? "User" : UsernameText.Trim();
        if (!TryParseSalary(out _))
            return SettingsOperationResult.Failure("Salary must be a valid amount.");

        await using var unitOfWork = _unitOfWorkFactory();
        await UpsertUserSettingAsync(unitOfWork, UserSettingNames.PreferredDisplayName, username);
        await UpsertUserSettingAsync(unitOfWork, UserSettingNames.Salary,
            string.IsNullOrWhiteSpace(SalaryText) ? null : ParseSalaryAmount().ToString(CultureInfo.InvariantCulture));
        await UpsertUserSettingAsync(unitOfWork, UserSettingNames.PreferredCurrencyCode, SelectedCurrencyCode);
        await unitOfWork.SaveChangesAsync();
        return SettingsOperationResult.Success();
    }

    private async Task<SettingsOperationResult> SaveBudgetAllocationAsync()
    {
        var total = NeedsAllocationPercentage + WantsAllocationPercentage + InvestAllocationPercentage;
        if (total != 100)
            return SettingsOperationResult.Failure(
                $"Needs, Wants, and Invest must add up to 100%. Current total: {total}%");

        await using var unitOfWork = _unitOfWorkFactory();
        await UpsertUserSettingAsync(unitOfWork, UserSettingNames.NeedsThreshold,
            NeedsAllocationPercentage.ToString(CultureInfo.InvariantCulture));
        await UpsertUserSettingAsync(unitOfWork, UserSettingNames.WantsThreshold,
            WantsAllocationPercentage.ToString(CultureInfo.InvariantCulture));
        await UpsertUserSettingAsync(unitOfWork, UserSettingNames.InvestThreshold,
            InvestAllocationPercentage.ToString(CultureInfo.InvariantCulture));
        await unitOfWork.SaveChangesAsync();
        return SettingsOperationResult.Success();
    }

    private async Task<SettingsOperationResult> SaveNotificationsAsync()
    {
        await using var unitOfWork = _unitOfWorkFactory();
        foreach (var notificationSetting in NotificationSettings)
            await UpsertUserSettingAsync(unitOfWork, notificationSetting.SettingName,
                notificationSetting.IsEnabled.ToString(CultureInfo.InvariantCulture));

        await unitOfWork.SaveChangesAsync();
        return SettingsOperationResult.Success();
    }

    private async Task SaveIsFirstRunAsync(bool isFirstRun)
    {
        await using var unitOfWork = _unitOfWorkFactory();
        await UpsertUserSettingAsync(unitOfWork, UserSettingNames.IsFirstRun, isFirstRun.ToString());
        await unitOfWork.SaveChangesAsync();
    }

    private async Task UpsertUserSettingAsync(IUnitOfWork unitOfWork, string name, string? value)
    {
        var existingSetting = await unitOfWork.UserSettings.GetByNameAsync(name);

        if (value is null)
        {
            if (existingSetting is not null)
                unitOfWork.UserSettings.Remove(existingSetting);

            return;
        }

        if (existingSetting is null)
        {
            await unitOfWork.UserSettings.AddAsync(new UserSettings { Name = name, Value = value });
            return;
        }

        existingSetting.Value = value;
        unitOfWork.UserSettings.Update(existingSetting);
    }

    private bool TryParseSalary(out decimal salary)
    {
        salary = 0m;
        if (string.IsNullOrWhiteSpace(SalaryText))
            return true;

        var normalized = SalaryText
            .Trim()
            .Replace(CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol, string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();

        return decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                   CultureInfo.CurrentCulture, out salary) ||
               decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                   CultureInfo.InvariantCulture, out salary);
    }

    private decimal ParseSalaryAmount()
    {
        return TryParseSalary(out var salary) ? salary : 0m;
    }

    private string BuildAllocationAmountText(int percentage)
    {
        var amount = decimal.Round(TotalBudgetAmount * percentage / 100m, 2);
        return $"{SelectedCurrencySymbol}{amount.ToString("N2", CultureInfo.InvariantCulture)}";
    }

    private static IReadOnlyList<SettingsCurrencyOptionVM> BuildCurrencyOptions()
    {
        return
        [
            new SettingsCurrencyOptionVM("USD", "US Dollar", "$"),
            new SettingsCurrencyOptionVM("EUR", "Euro", "EUR"),
            new SettingsCurrencyOptionVM("GBP", "British Pound", "GBP"),
            new SettingsCurrencyOptionVM("JPY", "Japanese Yen", "JPY"),
            new SettingsCurrencyOptionVM("THB", "Thai Baht", "THB"),
            new SettingsCurrencyOptionVM("AUD", "Australian Dollar", "A$"),
            new SettingsCurrencyOptionVM("CAD", "Canadian Dollar", "C$"),
            new SettingsCurrencyOptionVM("SGD", "Singapore Dollar", "S$"),
            new SettingsCurrencyOptionVM("VND", "Vietnamese Dong", "VND"),
            new SettingsCurrencyOptionVM("INR", "Indian Rupee", "INR")
        ];
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
            collection.Add(item);
    }

    private string ParseCurrencyCode(IReadOnlyDictionary<string, string> settings, string name, string defaultValue)
    {
        var code = ParseString(settings, name, defaultValue).ToUpperInvariant();
        if (CurrencyOptions.Any(option => string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase)))
            return code;

        return defaultValue;
    }

    private static string ParseString(IReadOnlyDictionary<string, string> settings, string name, string defaultValue)
    {
        if (!settings.TryGetValue(name, out var value))
            return defaultValue;

        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0 ? defaultValue : trimmed;
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string> settings, string name, bool defaultValue)
    {
        return settings.TryGetValue(name, out var value) && bool.TryParse(value, out var parsedValue)
            ? parsedValue
            : defaultValue;
    }

    private static int ParsePercentage(IReadOnlyDictionary<string, string> settings, string name, decimal defaultValue)
    {
        if (!settings.TryGetValue(name, out var value) ||
            !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue))
            return (int)defaultValue;

        return (int)Math.Round(parsedValue, MidpointRounding.AwayFromZero);
    }
}

public sealed record StartupWizardSpendingSourceItemVM(
    int Id,
    string Name,
    string TypeLabel,
    decimal PrimaryAmount,
    string PrimaryAmountLabel)
{
    public StartupWizardSpendingSourceItemVM(SpendingSource spendingSource) : this(
        spendingSource.Id,
        spendingSource.Name,
        spendingSource.SpendingSourceType switch
        {
            SpendingSourceType.Credit => "Credit",
            SpendingSourceType.BNPL => "BNPL",
            SpendingSourceType.Checking => "Checking",
            SpendingSourceType.Cash => "Cash",
            SpendingSourceType.Saving => "Savings",
            _ => "Source"
        },
        spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
            ? spendingSource.SpentAmount
            : spendingSource.Balance,
        spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
            ? "Spent"
            : "Balance")
    {
    }
}

public sealed record StartupWizardFixedExpenseItemVM(
    int Id,
    string Name,
    decimal Amount,
    string CategoryLabel,
    string SpendingSourceName,
    DateTime? DueDate)
{
    public StartupWizardFixedExpenseItemVM(Expense expense) : this(
        expense.Id,
        expense.Name,
        expense.Amount,
        expense.ExpenseCategory switch
        {
            ExpenseCategory.Needs => "Needs",
            ExpenseCategory.Wants => "Wants",
            _ => "Invest"
        },
        expense.SpendingSource?.Name ?? "No source",
        expense.RecurringDate)
    {
    }
}

public sealed record StartupWizardSavingGoalItemVM(
    int Id,
    string Name,
    decimal CurrentAmount,
    decimal TargetAmount,
    DateTime SavingEndDate)
{
    public StartupWizardSavingGoalItemVM(SavingGoal goal) : this(
        goal.Id,
        goal.Name,
        goal.CurrentAmount,
        goal.TargetAmount,
        goal.SavingEndDate)
    {
    }
}
