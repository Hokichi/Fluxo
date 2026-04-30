using System.Globalization;
using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Messages;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Shell.Main;

public partial class SpentAllowancePanelVM : ObservableRecipient,
    IRecipient<DateRangeSelectionChangedMessage>,
    IRecipient<AllTimeViewModeMessage>,
    IRecipient<DashboardDataInvalidatedMessage>
{
    private readonly IDataOperationRunner _dataOperationRunner;
    private readonly IExpenseLogService _expenseLogService;
    private readonly IMapper _mapper;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly ISpendingSourceService _spendingSourceService;

    private List<ExpenseLogVM> _allExpenseLogs = [];
    private decimal _needsThreshold = 0.5m;
    private decimal _wantsThreshold = 0.3m;
    private (DateTime From, DateTime To)? _selectedRange;
    private List<SpendingSourceVM> _spendingSources = [];

    public SpentAllowancePanelVM(
        IExpenseLogService expenseLogService,
        ISpendingSourceService spendingSourceService,
        IDataOperationRunner dataOperationRunner,
        IMapper mapper,
        IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _expenseLogService = expenseLogService;
        _spendingSourceService = spendingSourceService;
        _dataOperationRunner = dataOperationRunner;
        _mapper = mapper;

        IsActive = true;
    }

    [ObservableProperty]
    private decimal _totalSpent;

    [ObservableProperty]
    private decimal _allowance;

    public void Receive(DateRangeSelectionChangedMessage message)
    {
        _selectedRange = message.Value;
        RefreshMetrics();
    }

    public void Receive(AllTimeViewModeMessage message)
    {
        _selectedRange = null;
        RefreshMetrics();
    }

    public void Receive(DashboardDataInvalidatedMessage message)
    {
        if (!message.Value.HasFlag(DashboardDataInvalidationScope.Budget))
            return;

        _ = ReloadFromServicesAsync();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await LoadUserSettingsAsync(cancellationToken);

        var expenseLogs = _mapper.Map<IReadOnlyList<ExpenseLogVM>>(
            await _expenseLogService.GetAllAsync(cancellationToken));
        var spendingSources = _mapper.Map<IReadOnlyList<SpendingSourceVM>>(
            await _spendingSourceService.GetAllAsync(cancellationToken));

        _allExpenseLogs = expenseLogs
            .Where(log => !log.IsForDeletion)
            .OrderByDescending(log => log.DeductedOn)
            .ToList();
        _spendingSources = spendingSources.ToList();

        RefreshMetrics();
    }

    private async Task ReloadFromServicesAsync()
    {
        await _reloadGate.WaitAsync();

        try
        {
            await LoadAsync();
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    private void RefreshMetrics()
    {
        var visibleExpenseLogs = _selectedRange is { } range
            ? _allExpenseLogs.Where(log => log.DeductedOn.Date >= range.From.Date && log.DeductedOn.Date <= range.To.Date)
            : _allExpenseLogs;

        TotalSpent = visibleExpenseLogs.Sum(log => log.Amount);

        var totalIncomeAmount = _spendingSources.Where(source => source.IsEnabled).Sum(source => source.Balance);
        Allowance = CalculateDailyAllowance(totalIncomeAmount);
    }

    private decimal CalculateDailyAllowance(decimal totalIncomeAmount)
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var monthNeedsWantsSpent = _allExpenseLogs
            .Where(log => log.DeductedOn.Date >= monthStart && log.DeductedOn.Date <= monthEnd)
            .Where(log =>
                log.Expense?.ExpenseCategory == ExpenseCategory.Needs ||
                log.Expense?.ExpenseCategory == ExpenseCategory.Wants)
            .Sum(log => log.Amount);

        var needsWantsBudget = totalIncomeAmount * (_needsThreshold + _wantsThreshold);
        var remainingNeedsWants = needsWantsBudget - monthNeedsWantsSpent;

        return decimal.Round(remainingNeedsWants / 30, 2, MidpointRounding.AwayFromZero);
    }

    private async Task LoadUserSettingsAsync(CancellationToken cancellationToken)
    {
        var settingsByName = await _dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var settings = await scope.UnitOfWork.UserSettings.GetAllAsync(ct);
            return settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);
        }, cancellationToken);

        _needsThreshold = ParsePercentage(settingsByName, UserSettingNames.NeedsThreshold, 50m);
        _wantsThreshold = ParsePercentage(settingsByName, UserSettingNames.WantsThreshold, 30m);
    }

    private static decimal ParsePercentage(IReadOnlyDictionary<string, string> settings, string name, decimal defaultValue)
    {
        var percentageValue = ParseDecimal(settings, name, defaultValue);
        return percentageValue / 100m;
    }

    private static decimal ParseDecimal(IReadOnlyDictionary<string, string> settings, string name, decimal defaultValue)
    {
        if (settings.TryGetValue(name, out var value) &&
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue))
            return parsedValue;

        return defaultValue;
    }
}
