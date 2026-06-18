using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Budgeting;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.ViewModels.Entities;
using CoreILogMemoryAction = Fluxo.Core.Interfaces.History.ILogMemoryAction;

namespace Fluxo.ViewModels.Shell.Main;

public partial class AllocationDataVM : ObservableRecipient,
    IRecipient<DashboardDataInvalidatedMessage>,
    IRecipient<RecordLogMemoryMessage>,
    IRecipient<LogMemoryActionAppliedMessage>
{
    private readonly IDataOperationRunner _dataOperationRunner;
    private readonly IExpenseLogService _expenseLogService;
    private readonly IMapper _mapper;
    private readonly IAccountService _accountService;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);

    private List<ExpenseLogVM> _allExpenseLogs = [];
    private List<IncomeLogVM> _allIncomeLogs = [];
    private List<AccountVM> _accounts = [];
    private BudgetAllocation _budgetAllocation = new();

    public AllocationDataVM(
        IExpenseLogService expenseLogService,
        IAccountService accountService,
        IDataOperationRunner dataOperationRunner,
        IMapper mapper,
        IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _expenseLogService = expenseLogService;
        _accountService = accountService;
        _dataOperationRunner = dataOperationRunner;
        _mapper = mapper;

        IsActive = true;
    }

    [ObservableProperty]
    private decimal _totalIncomeAmount;

    [ObservableProperty]
    private decimal _totalSpent;

    [ObservableProperty]
    private int _dailyAllowance;

    [ObservableProperty]
    private decimal _needsAvailable;

    [ObservableProperty]
    private decimal _wantsAvailable;

    [ObservableProperty]
    private decimal _investAvailable;

    [ObservableProperty]
    private decimal _needsSpent;

    [ObservableProperty]
    private decimal _wantsSpent;

    [ObservableProperty]
    private decimal _investSpent;

    [ObservableProperty]
    private decimal _needsRemaining;

    [ObservableProperty]
    private decimal _wantsRemaining;

    [ObservableProperty]
    private decimal _investRemaining;

    [ObservableProperty]
    private int _needsPercentage;

    [ObservableProperty]
    private int _wantsPercentage;

    [ObservableProperty]
    private int _investPercentage;

    [ObservableProperty]
    private decimal _needsThreshold = 0.5m;

    [ObservableProperty]
    private decimal _wantsThreshold = 0.3m;

    [ObservableProperty]
    private decimal _investThreshold = 0.2m;

    public int NeedsAllocationPercentage => ConvertThresholdToPercentage(NeedsThreshold);

    public int WantsAllocationPercentage => ConvertThresholdToPercentage(WantsThreshold);

    public int InvestAllocationPercentage => ConvertThresholdToPercentage(InvestThreshold);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _budgetAllocation = await LoadBudgetAllocationAsync(cancellationToken);
        NeedsThreshold = _budgetAllocation.NeedsThreshold / 100m;
        WantsThreshold = _budgetAllocation.WantsThreshold / 100m;
        InvestThreshold = _budgetAllocation.InvestThreshold / 100m;

        _allExpenseLogs = _mapper.Map<IReadOnlyList<ExpenseLogVM>>(
                await _expenseLogService.GetAllAsync(cancellationToken))
            .Where(log => !log.IsForDeletion)
            .OrderByDescending(log => log.DeductedOn)
            .ToList();
        _allIncomeLogs = (await LoadIncomeLogsAsync(cancellationToken))
            .OrderByDescending(log => log.AddedOn)
            .ToList();
        _accounts = _mapper.Map<IReadOnlyList<AccountVM>>(
                await _accountService.GetAllAsync(cancellationToken))
            .ToList();

        RefreshBudgetMetrics();
    }

    public void Receive(DashboardDataInvalidatedMessage message)
    {
        if (!message.Value.HasFlag(DashboardDataInvalidationScope.Budget))
            return;

        _ = ReloadFromServicesAsync();
    }

    public void Receive(RecordLogMemoryMessage message)
    {
        ApplyLogMemoryAction(message.Value, LogMemoryApplyDirection.Redo);
    }

    public void Receive(LogMemoryActionAppliedMessage message)
    {
        var (action, direction) = message.Value;
        ApplyLogMemoryAction(action, direction);
    }

    partial void OnNeedsThresholdChanged(decimal value)
    {
        OnPropertyChanged(nameof(NeedsAllocationPercentage));
    }

    partial void OnWantsThresholdChanged(decimal value)
    {
        OnPropertyChanged(nameof(WantsAllocationPercentage));
    }

    partial void OnInvestThresholdChanged(decimal value)
    {
        OnPropertyChanged(nameof(InvestAllocationPercentage));
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

    private async Task<IReadOnlyList<IncomeLogVM>> LoadIncomeLogsAsync(CancellationToken cancellationToken)
    {
        return await _dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var incomeLogs = await scope.UnitOfWork.IncomeLogs.GetAllAsync(ct);
            return incomeLogs
                .Select(log => new IncomeLogVM
                {
                    Id = log.Id,
                    Name = log.Name,
                    Amount = log.Amount,
                    AddedOn = log.AddedOn,
                    Notes = log.Notes,
                    Account = new AccountVM
                    {
                        Id = log.AccountId,
                        Name = log.Account?.Name ?? string.Empty,
                        AccountType = log.Account?.AccountType ?? AccountType.Checking
                    }
                })
                .ToList();
        }, cancellationToken);
    }

    private async Task<BudgetAllocation> LoadBudgetAllocationAsync(CancellationToken cancellationToken)
    {
        return await _dataOperationRunner.RunAsync(async (scope, ct) =>
            await scope.UnitOfWork.BudgetAllocation.GetAsync(ct) ?? new BudgetAllocation(), cancellationToken);
    }

    private void RefreshBudgetMetrics()
    {
        TotalIncomeAmount = CalculateBudgetAvailableBase();
        var snapshot = BudgetAllocationCalculator.CalculateSnapshot(
            _budgetAllocation,
            CalculateSpentByCategory(BudgetAllocationCalculator.ResolveCurrentPeriod(
                _budgetAllocation.AllocationPeriod,
                DateTime.Today,
                _budgetAllocation.PeriodStart)),
            CalculateSpentByCategory(BudgetAllocationCalculator.ResolvePreviousPeriod(
                _budgetAllocation.AllocationPeriod,
                DateTime.Today,
                _budgetAllocation.PeriodStart)),
            DateTime.Today,
            TotalIncomeAmount);

        NeedsAvailable = snapshot.Needs.Available;
        WantsAvailable = snapshot.Wants.Available;
        InvestAvailable = snapshot.Invest.Available;

        NeedsSpent = snapshot.Needs.Spent;
        WantsSpent = snapshot.Wants.Spent;
        InvestSpent = snapshot.Invest.Spent;
        TotalSpent = NeedsSpent + WantsSpent + InvestSpent;

        NeedsRemaining = snapshot.Needs.Remaining;
        WantsRemaining = snapshot.Wants.Remaining;
        InvestRemaining = snapshot.Invest.Remaining;

        NeedsPercentage = snapshot.Needs.Percentage;
        WantsPercentage = snapshot.Wants.Percentage;
        InvestPercentage = snapshot.Invest.Percentage;
        DailyAllowance = (int)snapshot.DailyAllowance;
    }

    private IReadOnlyDictionary<ExpenseCategory, decimal> CalculateSpentByCategory(BudgetAllocationPeriod snapshotPeriod)
    {
        return BudgetEffectiveExpenseLogFilter.SelectBudgetEffectiveLogs(_allExpenseLogs)
            .Where(log => log.DeductedOn.Date >= snapshotPeriod.Start && log.DeductedOn.Date <= snapshotPeriod.End)
            .Where(log => log.Expense is not null)
            .GroupBy(log => log.Expense!.ExpenseCategory)
            .ToDictionary(group => group.Key, group => group.Sum(log => log.Amount));
    }

    private decimal CalculateBudgetAvailableBase()
    {
        if (_budgetAllocation.AllocationLimit > 0m)
            return _budgetAllocation.AllocationLimit;

        var balanceBackedSourceIds = _accounts
            .Where(IsBalanceBackedSource)
            .Select(source => source.Id)
            .ToHashSet();

        var balanceBackedExpenseAmount = BudgetEffectiveExpenseLogFilter.SelectBudgetEffectiveLogs(_allExpenseLogs)
            .Where(log => log.Account is { } source && balanceBackedSourceIds.Contains(source.Id))
            .Sum(log => log.Amount);

        return _accounts.Sum(source => source.Balance) + balanceBackedExpenseAmount;
    }

    private void ApplyLogMemoryAction(CoreILogMemoryAction action, LogMemoryApplyDirection direction)
    {
        switch (action)
        {
            case CompositeLogMemoryAction compositeAction:
                foreach (var childAction in compositeAction.Actions)
                    ApplyLogMemoryAction(childAction, direction);
                break;
            case AddExpenseLogMemoryAction addExpenseAction:
                ApplyExpenseAction(addExpenseAction.Snapshot, direction);
                break;
            case AddIncomeLogMemoryAction addIncomeAction:
                ApplyIncomeAction(addIncomeAction.Snapshot, direction);
                break;
            case EditExpenseLogMemoryAction editExpenseAction:
                ApplyEditedExpenseAction(editExpenseAction, direction);
                break;
            case EditIncomeLogMemoryAction editIncomeAction:
                ApplyEditedIncomeAction(editIncomeAction, direction);
                break;
            case DeleteExpenseLogMemoryAction deleteExpenseAction:
                ApplyDeletedExpenseAction(deleteExpenseAction, direction);
                break;
            case DeleteIncomeLogMemoryAction deleteIncomeAction:
                ApplyDeletedIncomeAction(deleteIncomeAction, direction);
                break;
        }
    }

    private void ApplyExpenseAction(ExpenseLogMemorySnapshot snapshot, LogMemoryApplyDirection direction)
    {
        if (direction == LogMemoryApplyDirection.Redo)
        {
            UpsertExpenseLog(snapshot);
            ApplyExpenseToTrackedSource(snapshot);
        }
        else
        {
            RemoveExpenseLog(snapshot.ExpenseLogId);
            RestoreExpenseFromTrackedSource(snapshot);
        }

        RefreshBudgetMetrics();
    }

    private void ApplyIncomeAction(IncomeLogMemorySnapshot snapshot, LogMemoryApplyDirection direction)
    {
        if (direction == LogMemoryApplyDirection.Redo)
        {
            UpsertIncomeLog(snapshot);
            ApplyIncomeToTrackedSource(snapshot);
        }
        else
        {
            RemoveIncomeLog(snapshot.IncomeLogId);
            RestoreIncomeFromTrackedSource(snapshot);
        }

        RefreshBudgetMetrics();
    }

    private void ApplyEditedExpenseAction(EditExpenseLogMemoryAction action, LogMemoryApplyDirection direction)
    {
        var previous = direction == LogMemoryApplyDirection.Redo ? action.Before : action.After;
        var target = direction == LogMemoryApplyDirection.Redo ? action.After : action.Before;

        UpsertExpenseLog(target);
        RestoreExpenseFromTrackedSource(previous);
        ApplyExpenseToTrackedSource(target);
        RefreshBudgetMetrics();
    }

    private void ApplyEditedIncomeAction(EditIncomeLogMemoryAction action, LogMemoryApplyDirection direction)
    {
        var previous = direction == LogMemoryApplyDirection.Redo ? action.Before : action.After;
        var target = direction == LogMemoryApplyDirection.Redo ? action.After : action.Before;

        UpsertIncomeLog(target);
        RestoreIncomeFromTrackedSource(previous);
        ApplyIncomeToTrackedSource(target);
        RefreshBudgetMetrics();
    }

    private void ApplyDeletedExpenseAction(DeleteExpenseLogMemoryAction action, LogMemoryApplyDirection direction)
    {
        if (action.Snapshot is not { } snapshot)
            return;

        if (direction == LogMemoryApplyDirection.Redo)
        {
            RemoveExpenseLog(snapshot.ExpenseLogId);
            RestoreExpenseFromTrackedSource(snapshot);
        }
        else
        {
            UpsertExpenseLog(snapshot);
            ApplyExpenseToTrackedSource(snapshot);
        }

        RefreshBudgetMetrics();
    }

    private void ApplyDeletedIncomeAction(DeleteIncomeLogMemoryAction action, LogMemoryApplyDirection direction)
    {
        if (direction == LogMemoryApplyDirection.Redo)
        {
            RemoveIncomeLog(action.Snapshot.IncomeLogId);
            RestoreIncomeFromTrackedSource(action.Snapshot);
        }
        else
        {
            UpsertIncomeLog(action.Snapshot);
            ApplyIncomeToTrackedSource(action.Snapshot);
        }

        RefreshBudgetMetrics();
    }

    private void ApplyExpenseToTrackedSource(ExpenseLogMemorySnapshot snapshot)
    {
        var source = _accounts.FirstOrDefault(candidate => candidate.Id == snapshot.AccountId);
        if (source is null)
            return;

        if (source.AccountType is AccountType.Credit or AccountType.BNPL)
        {
            source.SpentAmount += snapshot.Amount;
            return;
        }

        source.Balance -= snapshot.Amount;
    }

    private void RestoreExpenseFromTrackedSource(ExpenseLogMemorySnapshot snapshot)
    {
        var source = _accounts.FirstOrDefault(candidate => candidate.Id == snapshot.AccountId);
        if (source is null)
            return;

        if (source.AccountType is AccountType.Credit or AccountType.BNPL)
        {
            source.SpentAmount = Math.Max(0m, source.SpentAmount - snapshot.Amount);
            return;
        }

        source.Balance += snapshot.Amount;
    }

    private void ApplyIncomeToTrackedSource(IncomeLogMemorySnapshot snapshot)
    {
        var source = _accounts.FirstOrDefault(candidate => candidate.Id == snapshot.AccountId);
        if (source is null)
            return;

        if (source.AccountType is AccountType.Credit or AccountType.BNPL)
        {
            source.SpentAmount = Math.Max(0m, source.SpentAmount - snapshot.Amount);
            return;
        }

        source.Balance += snapshot.Amount;
    }

    private void RestoreIncomeFromTrackedSource(IncomeLogMemorySnapshot snapshot)
    {
        var source = _accounts.FirstOrDefault(candidate => candidate.Id == snapshot.AccountId);
        if (source is null)
            return;

        if (source.AccountType is AccountType.Credit or AccountType.BNPL)
        {
            source.SpentAmount += snapshot.Amount;
            return;
        }

        source.Balance -= snapshot.Amount;
    }

    private void UpsertExpenseLog(ExpenseLogMemorySnapshot snapshot)
    {
        var existingIndex = _allExpenseLogs.FindIndex(log => log.Id == snapshot.ExpenseLogId);
        var vm = new ExpenseLogVM
        {
            Id = snapshot.ExpenseLogId,
            Amount = snapshot.Amount,
            DeductedOn = snapshot.DeductedOn,
            Notes = snapshot.Notes,
            IsForDeletion = snapshot.IsForDeletion,
            ParentLogId = snapshot.ParentLogId,
            Expense = new ExpenseVM
            {
                Id = snapshot.ExpenseId,
                Name = snapshot.ExpenseName,
                ExpenseCategory = snapshot.ExpenseCategory
            },
            Account = new AccountVM { Id = snapshot.AccountId }
        };

        if (existingIndex >= 0)
            _allExpenseLogs[existingIndex] = vm;
        else
            _allExpenseLogs.Add(vm);
    }

    private void RemoveExpenseLog(int expenseLogId)
    {
        _allExpenseLogs = _allExpenseLogs
            .Where(log => log.Id != expenseLogId)
            .ToList();
    }

    private void UpsertIncomeLog(IncomeLogMemorySnapshot snapshot)
    {
        var existingIndex = _allIncomeLogs.FindIndex(log => log.Id == snapshot.IncomeLogId);
        var vm = new IncomeLogVM
        {
            Id = snapshot.IncomeLogId,
            Name = snapshot.Name,
            Amount = snapshot.Amount,
            AddedOn = snapshot.AddedOn,
            Notes = snapshot.Notes,
            Account = new AccountVM { Id = snapshot.AccountId }
        };

        if (existingIndex >= 0)
            _allIncomeLogs[existingIndex] = vm;
        else
            _allIncomeLogs.Add(vm);
    }

    private void RemoveIncomeLog(int incomeLogId)
    {
        _allIncomeLogs = _allIncomeLogs
            .Where(log => log.Id != incomeLogId)
            .ToList();
    }

    private static bool IsBalanceBackedSource(AccountVM source)
    {
        return source.AccountType is not (AccountType.Credit or AccountType.BNPL);
    }

    private static int ConvertThresholdToPercentage(decimal threshold)
    {
        return (int)Math.Round(threshold * 100m, MidpointRounding.AwayFromZero);
    }
}
