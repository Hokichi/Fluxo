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

public partial class SpentAllowancePanelVM : ObservableRecipient,
    IRecipient<DateRangeSelectionChangedMessage>,
    IRecipient<AllTimeViewModeMessage>,
    IRecipient<DashboardDataInvalidatedMessage>,
    IRecipient<RecordLogMemoryMessage>,
    IRecipient<LogMemoryActionAppliedMessage>
{
    private readonly IDataOperationRunner _dataOperationRunner;
    private readonly IExpenseLogService _expenseLogService;
    private readonly IMapper _mapper;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly IAccountService _accountService;
    private readonly Func<DateTime> _todayProvider;

    private List<ExpenseLogVM> _allExpenseLogs = [];
    private List<IncomeLog> _allIncomeLogs = [];
    private BudgetAllocation _budgetAllocation = new();
    private (DateTime From, DateTime To)? _selectedRange;
    private List<AccountVM> _accounts = [];

    public SpentAllowancePanelVM(
        IExpenseLogService expenseLogService,
        IAccountService accountService,
        IDataOperationRunner dataOperationRunner,
        IMapper mapper,
        IMessenger? messenger = null,
        Func<DateTime>? todayProvider = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _expenseLogService = expenseLogService;
        _accountService = accountService;
        _dataOperationRunner = dataOperationRunner;
        _mapper = mapper;
        _todayProvider = todayProvider ?? (() => DateTime.Today);

        IsActive = true;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Net))]
    private decimal _totalSpent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Net))]
    private decimal _totalEarned;

    [ObservableProperty]
    private decimal _allowance;

    public decimal Net => TotalEarned - TotalSpent;

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

    public void Receive(RecordLogMemoryMessage message)
    {
        ApplyLogMemoryAction(message.Value, LogMemoryApplyDirection.Redo);
    }

    public void Receive(LogMemoryActionAppliedMessage message)
    {
        var (action, direction) = message.Value;
        ApplyLogMemoryAction(action, direction);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _budgetAllocation = await LoadBudgetAllocationAsync(cancellationToken);

        var expenseLogs = _mapper.Map<IReadOnlyList<ExpenseLogVM>>(
            await _expenseLogService.GetAllAsync(cancellationToken));
        var accounts = _mapper.Map<IReadOnlyList<AccountVM>>(
            await _accountService.GetAllAsync(cancellationToken));
        var incomeLogs = await LoadIncomeLogsAsync(cancellationToken);

        _allExpenseLogs = expenseLogs
            .Where(log => !log.IsForDeletion)
            .OrderByDescending(log => log.DeductedOn)
            .ToList();
        _allIncomeLogs = incomeLogs
            .OrderByDescending(log => log.AddedOn)
            .ToList();
        _accounts = accounts.ToList();

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
        var visibleIncomeLogs = _selectedRange is { } incomeRange
            ? _allIncomeLogs.Where(log => log.AddedOn.Date >= incomeRange.From.Date && log.AddedOn.Date <= incomeRange.To.Date)
            : _allIncomeLogs;

        TotalSpent = visibleExpenseLogs.Sum(log => log.Amount);
        TotalEarned = visibleIncomeLogs.Sum(log => log.Amount);

        var totalIncomeAmount = _accounts.Where(source => source.IsEnabled).Sum(source => source.Balance);
        Allowance = BudgetAllocationCalculator.CalculateDailyAllowance(
            _budgetAllocation,
            _todayProvider(),
            totalIncomeAmount);
    }

    private void ApplyLogMemoryAction(CoreILogMemoryAction action, LogMemoryApplyDirection direction)
    {
        switch (action)
        {
            case CompositeLogMemoryAction compositeAction:
                foreach (var childAction in compositeAction.Actions)
                    ApplyLogMemoryAction(childAction, direction);
                return;

            case DeleteExpenseLogMemoryAction deleteExpenseAction:
                ApplyDeletedExpenseAction(deleteExpenseAction, direction);
                return;
        }
    }

    private void ApplyDeletedExpenseAction(DeleteExpenseLogMemoryAction action, LogMemoryApplyDirection direction)
    {
        if (action.Snapshot is not { } snapshot)
            return;

        if (direction == LogMemoryApplyDirection.Redo)
        {
            RemoveExpenseLog(snapshot.ExpenseLogId);
            RestoreExpenseFromTrackedSource(snapshot);
            RefreshMetrics();
            return;
        }

        UpsertExpenseLog(snapshot);
        ApplyExpenseToTrackedSource(snapshot);
        RefreshMetrics();
    }

    private void RemoveExpenseLog(int expenseLogId)
    {
        _allExpenseLogs = _allExpenseLogs
            .Where(log => log.Id != expenseLogId)
            .ToList();
    }

    private void UpsertExpenseLog(ExpenseLogMemorySnapshot snapshot)
    {
        var existingIndex = _allExpenseLogs.FindIndex(log => log.Id == snapshot.ExpenseLogId);
        var vm = ToExpenseLogVm(snapshot);

        if (existingIndex >= 0)
            _allExpenseLogs[existingIndex] = vm;
        else
            _allExpenseLogs.Add(vm);
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

    private ExpenseLogVM ToExpenseLogVm(ExpenseLogMemorySnapshot snapshot)
    {
        var source = _accounts.FirstOrDefault(candidate => candidate.Id == snapshot.AccountId);

        return new ExpenseLogVM
        {
            Id = snapshot.ExpenseLogId,
            Amount = snapshot.Amount,
            DeductedOn = snapshot.DeductedOn,
            Notes = snapshot.Notes,
            IsForDeletion = snapshot.IsForDeletion,
            Account = new AccountVM
            {
                Id = snapshot.AccountId,
                Name = source?.Name ?? string.Empty,
                AccountType = source?.AccountType ?? AccountType.Checking
            },
            Expense = new ExpenseVM
            {
                Id = snapshot.ExpenseId,
                Name = snapshot.ExpenseName,
                Amount = snapshot.Amount,
                ExpenseCategory = snapshot.ExpenseCategory
            }
        };
    }

    private async Task<BudgetAllocation> LoadBudgetAllocationAsync(CancellationToken cancellationToken)
    {
        return await _dataOperationRunner.RunAsync(async (scope, ct) =>
            await scope.UnitOfWork.BudgetAllocation.GetAsync(ct) ?? new BudgetAllocation(), cancellationToken);
    }

    private async Task<IReadOnlyList<IncomeLog>> LoadIncomeLogsAsync(CancellationToken cancellationToken)
    {
        return await _dataOperationRunner.RunAsync(
            async (scope, ct) => await scope.UnitOfWork.IncomeLogs.GetAllAsync(ct),
            cancellationToken);
    }
}
