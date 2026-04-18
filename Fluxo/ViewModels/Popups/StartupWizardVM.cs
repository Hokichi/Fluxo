using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Converters;
using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.ViewModels.Helpers;
using Fluxo.ViewModels.Messages;
using Fluxo.ViewModels.Shell;

namespace Fluxo.ViewModels.Popups;

public partial class StartupWizardVM : ObservableObject
{
    private const string DefaultCurrencyCode = "USD";

    private readonly MainVM _mainViewModel;
    private readonly IUnitOfWork _unitOfWork;

    [ObservableProperty] private int _currentStepIndex;
    [ObservableProperty] private string _budgetAllocationErrorMessage = string.Empty;
    [ObservableProperty] private int _investAllocationPercentage = 20;
    [ObservableProperty] private int _needsAllocationPercentage = 50;
    [ObservableProperty] private string _selectedCurrencyCode = DefaultCurrencyCode;
    [ObservableProperty] private string _usernameText = "User";
    [ObservableProperty] private int _wantsAllocationPercentage = 30;

    public StartupWizardVM(MainVM mainViewModel, IUnitOfWork unitOfWork)
    {
        _mainViewModel = mainViewModel;
        _unitOfWork = unitOfWork;

        foreach (var option in BuildCurrencyOptions())
            CurrencyOptions.Add(option);

        for (var i = 0; i < TotalSteps; i++)
            StepDots.Add(new WizardStepDotVM(i, i == 0));
    }

    public ObservableCollection<StartupWizardSpendingSourceItemVM> SpendingSources { get; } = [];
    public ObservableCollection<StartupWizardFixedExpenseItemVM> FixedExpenses { get; } = [];
    public ObservableCollection<StartupWizardSavingGoalItemVM> SavingGoals { get; } = [];
    public ObservableCollection<SettingsNotificationOptionVM> NotificationSettings { get; } = [];
    public ObservableCollection<SettingsCurrencyOptionVM> CurrencyOptions { get; } = [];
    public ObservableCollection<WizardStepDotVM> StepDots { get; } = [];

    public int TotalSteps => 10;
    public bool IsGreetingStep => CurrentStepIndex == 0;
    public bool IsNameStep => CurrentStepIndex == 1;
    public bool IsMiddleStep => CurrentStepIndex >= 2 && CurrentStepIndex <= 7;
    public bool IsLoadingStep => CurrentStepIndex == 8;
    public bool IsFinalStep => CurrentStepIndex == TotalSteps - 1;
    public bool IsStep2Active => CurrentStepIndex == 2;
    public bool IsStep3Active => CurrentStepIndex == 3;
    public bool IsStep4Active => CurrentStepIndex == 4;
    public bool IsStep5Active => CurrentStepIndex == 5;
    public bool IsStep6Active => CurrentStepIndex == 6;
    public bool IsStep7Active => CurrentStepIndex == 7;
    public string StepCounterText => IsMiddleStep ? $"Step {CurrentStepIndex - 1} of 6" : string.Empty;
    public decimal TotalBudgetAmount => CalculateTotalBudgetAmount();
    public string SelectedCurrencySymbol =>
        CurrencyOptions.FirstOrDefault(option =>
            string.Equals(option.Code, SelectedCurrencyCode, StringComparison.OrdinalIgnoreCase))?.Symbol ?? "$";
    public bool HasBudgetAllocationError => !string.IsNullOrWhiteSpace(BudgetAllocationErrorMessage);
    public string NeedsAllocationAmountText => BuildAllocationAmountText(NeedsAllocationPercentage);
    public string WantsAllocationAmountText => BuildAllocationAmountText(WantsAllocationPercentage);
    public string InvestAllocationAmountText => BuildAllocationAmountText(InvestAllocationPercentage);
    public string ResolvedUsername => string.IsNullOrWhiteSpace(UsernameText) ? "User" : UsernameText.Trim();

    public string CurrentStepTitle => CurrentStepIndex switch
    {
        0 => "Let's get started",
        1 => "What should Fluxo call you?",
        2 => "Add spending sources",
        3 => "Add fixed expenses",
        4 => "Add savings goals",
        5 => "Budget allocation",
        6 => "Notification preferences",
        7 => "Setup summary",
        8 => "Getting things ready",
        _ => "Welcome to Fluxo"
    };

    public string CurrentStepDescription => CurrentStepIndex switch
    {
        0 => "Fluxo helps you take control of your finances with smart budgeting, spending tracking, and savings goals.",
        1 => "Pick the name you'd like Fluxo to use throughout the app.",
        2 => "Add the accounts and sources you spend from most often.",
        3 => "Add recurring fixed expenses so Fluxo can account for them upfront.",
        4 => "Add a few goals to start tracking progress right away.",
        5 => "Split your budget into Needs, Wants, and Invest.",
        6 => "Choose which reminders and alerts Fluxo should show.",
        7 => "Here's a summary of everything you've set up.",
        8 => "Fluxo is loading your data. This will only take a moment.",
        _ => $"You're ready, {ResolvedUsername}. Fluxo is all set to open."
    };

    partial void OnCurrentStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsGreetingStep));
        OnPropertyChanged(nameof(IsNameStep));
        OnPropertyChanged(nameof(IsMiddleStep));
        OnPropertyChanged(nameof(IsLoadingStep));
        OnPropertyChanged(nameof(IsFinalStep));
        OnPropertyChanged(nameof(IsStep2Active));
        OnPropertyChanged(nameof(IsStep3Active));
        OnPropertyChanged(nameof(IsStep4Active));
        OnPropertyChanged(nameof(IsStep5Active));
        OnPropertyChanged(nameof(IsStep6Active));
        OnPropertyChanged(nameof(IsStep7Active));
        OnPropertyChanged(nameof(StepCounterText));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(CurrentStepDescription));
        OnPropertyChanged(nameof(IsNextEnabled));

        foreach (var dot in StepDots)
            dot.IsActive = dot.StepIndex == value;

        if (value == 7)
            RefreshReportProperties();
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
        ValidateBudgetAllocation();
    }

    partial void OnWantsAllocationPercentageChanged(int value)
    {
        OnPropertyChanged(nameof(WantsAllocationAmountText));
        ValidateBudgetAllocation();
    }

    partial void OnInvestAllocationPercentageChanged(int value)
    {
        OnPropertyChanged(nameof(InvestAllocationAmountText));
        ValidateBudgetAllocation();
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
        if (CurrentStepIndex <= 0)
            return;

        if (IsFinalStep)
        {
            CurrentStepIndex = 6;
            return;
        }

        if (CurrentStepIndex == 5 && !HasSpendingSources)
        {
            CurrentStepIndex = 2;
            return;
        }

        CurrentStepIndex--;
    }

    public void NavigateToStep(int stepIndex)
    {
        if (stepIndex >= 0 && stepIndex < TotalSteps && stepIndex != CurrentStepIndex)
            CurrentStepIndex = stepIndex;
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

    public async Task InitializeMainViewModelAsync()
    {
        await _mainViewModel.Initialize();
    }

    public async Task<SettingsOperationResult> CompleteAsync()
    {
        await SaveIsFirstRunAsync(false);
        return SettingsOperationResult.Success();
    }

    public async Task<SettingsOperationResult> DismissAsync()
    {
        var result = await PersistCurrentStepAsync();
        if (!result.IsSuccess)
            return result;

        await SaveIsFirstRunAsync(false);

        if (!_mainViewModel.IsInitialized)
            await _mainViewModel.Initialize();
        else
            await _mainViewModel.ReloadCurrentDataAsync();

        return SettingsOperationResult.Success();
    }

    public AddSpendingSourceVM CreateAddSpendingSourceViewModel()
    {
        return new AddSpendingSourceVM(_mainViewModel, _unitOfWork);
    }

    public async Task<AddSpendingSourceVM> CreateEditSpendingSourceViewModelAsync(int id)
    {
        var unitOfWork = _unitOfWork;
        var source = await unitOfWork.SpendingSources.GetByIdAsync(id);
        if (source is null)
            return CreateAddSpendingSourceViewModel();

        var vm = new AddSpendingSourceVM(_mainViewModel, _unitOfWork) { EditingId = source.Id };
        vm.NameText = source.Name;
        vm.SelectedSpendingSourceType = source.SpendingSourceType;
        vm.ShowOnUI = source.ShowOnUI;
        vm.IsEnabled = source.IsEnabled;

        if (source.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            vm.PrimaryAmountText = source.SpentAmount.ToString("N2", CultureInfo.InvariantCulture);
            vm.SpentAmountText = source.SpentAmount.ToString("N2", CultureInfo.InvariantCulture);
            vm.AccountLimitText = source.AccountLimit.ToString("N2", CultureInfo.InvariantCulture);
            vm.MonthlyDueDateText = MonthlyDueDateHelper.Normalize(source.MonthlyDueDate)?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }
        else
        {
            vm.PrimaryAmountText = source.Balance.ToString("N2", CultureInfo.InvariantCulture);
        }

        if (source.SpendingSourceType == SpendingSourceType.Saving && source.InterestRate.HasValue)
            vm.ApyText = source.InterestRate.Value.ToString("N2", CultureInfo.InvariantCulture);

        return vm;
    }

    public AddFixedExpenseVM CreateAddFixedExpenseViewModel()
    {
        return new AddFixedExpenseVM(_mainViewModel, _unitOfWork);
    }

    public async Task<AddFixedExpenseVM> CreateEditFixedExpenseViewModelAsync(int id)
    {
        var unitOfWork = _unitOfWork;
        var expense = await unitOfWork.Expenses.GetByIdAsync(id);
        if (expense is null)
            return CreateAddFixedExpenseViewModel();

        var vm = new AddFixedExpenseVM(_mainViewModel, _unitOfWork) { EditingId = expense.Id };
        vm.NameText = expense.Name;
        vm.AmountText = expense.Amount.ToString("N2", CultureInfo.InvariantCulture);
        vm.SelectedCategory = expense.ExpenseCategory;
        vm.RecurringDateText = MonthlyDueDateHelper.Normalize(expense.RecurringDate)?.ToString(CultureInfo.InvariantCulture) ??
                               MonthlyDueDateHelper.Normalize(DateTime.Today.Day)?.ToString(CultureInfo.InvariantCulture) ??
                               MonthlyDueDateHelper.MinMonthlyDay.ToString(CultureInfo.InvariantCulture);
        vm.IsActive = expense.IsActive;

        if (expense.SpendingSourceId > 0)
        {
            var matchingSource = vm.SpendingSources.FirstOrDefault(s => s.Id == expense.SpendingSourceId);
            if (matchingSource is not null)
                vm.SelectedSpendingSource = matchingSource;
        }

        if (expense.ExpenseTag is not null)
            vm.TagNameText = expense.ExpenseTag.Name;

        return vm;
    }

    public AddSavingGoalVM CreateAddSavingGoalViewModel()
    {
        return new AddSavingGoalVM(_mainViewModel, _unitOfWork);
    }

    public async Task<AddSavingGoalVM> CreateEditSavingGoalViewModelAsync(int id)
    {
        var unitOfWork = _unitOfWork;
        var goal = await unitOfWork.SavingGoals.GetByIdAsync(id);
        if (goal is null)
            return CreateAddSavingGoalViewModel();

        return new AddSavingGoalVM(_mainViewModel, _unitOfWork)
        {
            EditingId = goal.Id,
            NameText = goal.Name,
            TargetAmountText = goal.TargetAmount.ToString("N2", CultureInfo.InvariantCulture),
            CurrentAmountText = goal.CurrentAmount.ToString("N2", CultureInfo.InvariantCulture),
            EndDate = goal.SavingEndDate
        };
    }

    public async Task DeleteSpendingSourceAsync(int id)
    {
        var unitOfWork = _unitOfWork;
        var source = await unitOfWork.SpendingSources.GetByIdAsync(id);
        if (source is not null)
        {
            unitOfWork.SpendingSources.Remove(source);
            await unitOfWork.SaveChangesAsync();
            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));
        }

        await RefreshSpendingSourcesAsync();
    }

    public async Task DeleteFixedExpenseAsync(int id)
    {
        var unitOfWork = _unitOfWork;
        var expense = await unitOfWork.Expenses.GetByIdAsync(id);
        if (expense is not null)
        {
            unitOfWork.Expenses.Remove(expense);
            await unitOfWork.SaveChangesAsync();
            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));
        }

        await RefreshFixedExpensesAsync();
    }

    public async Task DeleteSavingGoalAsync(int id)
    {
        var unitOfWork = _unitOfWork;
        var goal = await unitOfWork.SavingGoals.GetByIdAsync(id);
        if (goal is not null)
        {
            unitOfWork.SavingGoals.Remove(goal);
            await unitOfWork.SaveChangesAsync();
            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.SavingGoals));
        }

        await RefreshSavingGoalsAsync();
    }

    public async Task RefreshCollectionsAsync()
    {
        await RefreshSpendingSourcesAsync();
        await RefreshFixedExpensesAsync();
        await RefreshSavingGoalsAsync();
    }

    public async Task RefreshSpendingSourcesAsync()
    {
        var unitOfWork = _unitOfWork;
        ReplaceCollection(SpendingSources, (await unitOfWork.SpendingSources.GetAllAsync())
            .OrderBy(source => source.Name)
            .Select(source => new StartupWizardSpendingSourceItemVM(source)));

        OnPropertyChanged(nameof(TotalBudgetAmount));
        OnPropertyChanged(nameof(NeedsAllocationAmountText));
        OnPropertyChanged(nameof(WantsAllocationAmountText));
        OnPropertyChanged(nameof(InvestAllocationAmountText));
        OnPropertyChanged(nameof(HasSpendingSources));
    }

    public async Task RefreshFixedExpensesAsync()
    {
        var unitOfWork = _unitOfWork;
        ReplaceCollection(FixedExpenses, (await unitOfWork.Expenses.GetAllAsync())
            .Where(expense => expense.ExpenseKind == ExpenseKind.Fixed)
            .OrderBy(expense => expense.Name)
            .Select(expense => new StartupWizardFixedExpenseItemVM(expense)));
    }

    public async Task RefreshSavingGoalsAsync()
    {
        var unitOfWork = _unitOfWork;
        ReplaceCollection(SavingGoals, (await unitOfWork.SavingGoals.GetAllAsync())
            .OrderBy(goal => goal.SavingEndDate)
            .ThenBy(goal => goal.Name)
            .Select(goal => new StartupWizardSavingGoalItemVM(goal)));
    }

    private async Task LoadSettingsAsync()
    {
        var unitOfWork = _unitOfWork;
        var settings = await unitOfWork.UserSettings.GetAllAsync();
        var settingsByName = settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);

        UsernameText = ParseString(settingsByName, UserSettingNames.PreferredDisplayName, "User");
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
            1 => await SavePersonalizationAsync(),
            5 => await SaveBudgetAllocationAsync(),
            6 => await SaveNotificationsAsync(),
            7 => PersistReportStep(),
            _ => SettingsOperationResult.Success()
        };
    }

    private SettingsOperationResult PersistReportStep()
    {
        RefreshReportProperties();
        return SettingsOperationResult.Success();
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

        var unitOfWork = _unitOfWork;
        await UpsertUserSettingAsync(unitOfWork, UserSettingNames.PreferredDisplayName, username);
        await UpsertUserSettingAsync(unitOfWork, UserSettingNames.PreferredCurrencyCode, SelectedCurrencyCode);
        await unitOfWork.SaveChangesAsync();
        WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
            DashboardDataInvalidationScope.All));
        return SettingsOperationResult.Success();
    }

    private void ValidateBudgetAllocation()
    {
        var total = NeedsAllocationPercentage + WantsAllocationPercentage + InvestAllocationPercentage;
        BudgetAllocationErrorMessage = total == 100
            ? string.Empty
            : $"Needs, Wants, and Invest must add up to 100%. Current total: {total}%";
        OnPropertyChanged(nameof(HasBudgetAllocationError));
        OnPropertyChanged(nameof(IsNextEnabled));
    }

    private async Task<SettingsOperationResult> SaveBudgetAllocationAsync()
    {
        var total = NeedsAllocationPercentage + WantsAllocationPercentage + InvestAllocationPercentage;
        if (total != 100)
            return SettingsOperationResult.Failure(
                $"Needs, Wants, and Invest must add up to 100%. Current total: {total}%");

        var unitOfWork = _unitOfWork;
        await UpsertUserSettingAsync(unitOfWork, UserSettingNames.NeedsThreshold,
            NeedsAllocationPercentage.ToString(CultureInfo.InvariantCulture));
        await UpsertUserSettingAsync(unitOfWork, UserSettingNames.WantsThreshold,
            WantsAllocationPercentage.ToString(CultureInfo.InvariantCulture));
        await UpsertUserSettingAsync(unitOfWork, UserSettingNames.InvestThreshold,
            InvestAllocationPercentage.ToString(CultureInfo.InvariantCulture));
        await unitOfWork.SaveChangesAsync();
        WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
            DashboardDataInvalidationScope.Budget));
        return SettingsOperationResult.Success();
    }

    private async Task<SettingsOperationResult> SaveNotificationsAsync()
    {
        var unitOfWork = _unitOfWork;
        foreach (var notificationSetting in NotificationSettings)
            await UpsertUserSettingAsync(unitOfWork, notificationSetting.SettingName,
                notificationSetting.IsEnabled.ToString(CultureInfo.InvariantCulture));

        await unitOfWork.SaveChangesAsync();
        WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
            DashboardDataInvalidationScope.Notifications));
        return SettingsOperationResult.Success();
    }

    private async Task SaveIsFirstRunAsync(bool isFirstRun)
    {
        var unitOfWork = _unitOfWork;
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

    private decimal CalculateTotalBudgetAmount()
    {
        return SpendingSources.Sum(source => source.PrimaryAmount);
    }

    public void IncrementAllocation(BudgetAllocationSegment segment, int delta)
    {
        switch (segment)
        {
            case BudgetAllocationSegment.Needs:
                NeedsAllocationPercentage = Math.Clamp(NeedsAllocationPercentage + delta, 0, 100);
                break;
            case BudgetAllocationSegment.Wants:
                WantsAllocationPercentage = Math.Clamp(WantsAllocationPercentage + delta, 0, 100);
                break;
            case BudgetAllocationSegment.Invest:
                InvestAllocationPercentage = Math.Clamp(InvestAllocationPercentage + delta, 0, 100);
                break;
        }
    }

    public bool HasSpendingSources => SpendingSources.Count > 0;
    public bool IsNextEnabled => !(IsStep5Active && HasBudgetAllocationError);

    public string ReportUsernameText => ResolvedUsername;
    public string ReportCurrencyText => SelectedCurrencyCode;
    public int ReportSpendingSourceCount => SpendingSources.Count;
    public int ReportFixedExpenseCount => FixedExpenses.Count;
    public int ReportSavingGoalCount => SavingGoals.Count;
    public string ReportTotalBalanceText =>
        MoneyFormatUtility.ToCompactText(SpendingSources.Sum(s => s.PrimaryAmount), CultureInfo.CurrentCulture);
    public string ReportTotalBalanceTooltipText =>
        MoneyFormatUtility.ToFullText(SpendingSources.Sum(s => s.PrimaryAmount), CultureInfo.CurrentCulture);
    public string ReportTotalFixedExpenseText =>
        MoneyFormatUtility.ToCompactText(FixedExpenses.Sum(e => e.Amount), CultureInfo.CurrentCulture);
    public string ReportTotalFixedExpenseTooltipText =>
        MoneyFormatUtility.ToFullText(FixedExpenses.Sum(e => e.Amount), CultureInfo.CurrentCulture);
    public string ReportBudgetAllocationText =>
        $"Needs {NeedsAllocationPercentage}% / Wants {WantsAllocationPercentage}% / Invest {InvestAllocationPercentage}%";
    public int ReportNotificationsEnabledCount => NotificationSettings.Count(n => n.IsEnabled);

    public void RefreshReportProperties()
    {
        OnPropertyChanged(nameof(ReportUsernameText));
        OnPropertyChanged(nameof(ReportCurrencyText));
        OnPropertyChanged(nameof(ReportSpendingSourceCount));
        OnPropertyChanged(nameof(ReportFixedExpenseCount));
        OnPropertyChanged(nameof(ReportSavingGoalCount));
        OnPropertyChanged(nameof(ReportTotalBalanceText));
        OnPropertyChanged(nameof(ReportTotalBalanceTooltipText));
        OnPropertyChanged(nameof(ReportTotalFixedExpenseText));
        OnPropertyChanged(nameof(ReportTotalFixedExpenseTooltipText));
        OnPropertyChanged(nameof(ReportBudgetAllocationText));
        OnPropertyChanged(nameof(ReportNotificationsEnabledCount));
    }

    private string BuildAllocationAmountText(int percentage)
    {
        var amount = decimal.Round(TotalBudgetAmount * percentage / 100m, 2);
        return amount.ToString("N2", CultureInfo.CurrentCulture);
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
    int? DueDate)
{
    public string DueDateDisplay => DueDate.HasValue ? $"Due day {DueDate.Value}" : "No due day";

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

public sealed partial class WizardStepDotVM : ObservableObject
{
    [ObservableProperty] private bool _isActive;

    public int StepIndex { get; }

    public WizardStepDotVM(int stepIndex, bool isActive)
    {
        StepIndex = stepIndex;
        _isActive = isActive;
    }
}


