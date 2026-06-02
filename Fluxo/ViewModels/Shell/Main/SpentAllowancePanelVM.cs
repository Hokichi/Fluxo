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
    private readonly ISpendingSourceService _spendingSourceService;
    private readonly Func<DateTime> _todayProvider;

    private List<ExpenseLogVM> _allExpenseLogs = [];
    private BudgetAllocation _budgetAllocation = new();
    private (DateTime From, DateTime To)? _selectedRange;
    private List<SpendingSourceVM> _spendingSources = [];

    public SpentAllowancePanelVM(
        IExpenseLogService expenseLogService,
        ISpendingSourceService spendingSourceService,
        IDataOperationRunner dataOperationRunner,
        IMapper mapper,
        IMessenger? messenger = null,
        Func<DateTime>? todayProvider = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _expenseLogService = expenseLogService;
        _spendingSourceService = spendingSourceService;
        _dataOperationRunner = dataOperationRunner;
        _mapper = mapper;
        _todayProvider = todayProvider ?? (() => DateTime.Today);

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
        var source = _spendingSources.FirstOrDefault(candidate => candidate.Id == snapshot.SpendingSourceId);
        if (source is null)
            return;

        if (source.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            source.SpentAmount += snapshot.Amount;
            return;
        }

        source.Balance -= snapshot.Amount;
    }

    private void RestoreExpenseFromTrackedSource(ExpenseLogMemorySnapshot snapshot)
    {
        var source = _spendingSources.FirstOrDefault(candidate => candidate.Id == snapshot.SpendingSourceId);
        if (source is null)
            return;

        if (source.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            source.SpentAmount = Math.Max(0m, source.SpentAmount - snapshot.Amount);
            return;
        }

        source.Balance += snapshot.Amount;
    }

    private ExpenseLogVM ToExpenseLogVm(ExpenseLogMemorySnapshot snapshot)
    {
        var source = _spendingSources.FirstOrDefault(candidate => candidate.Id == snapshot.SpendingSourceId);

        return new ExpenseLogVM
        {
            Id = snapshot.ExpenseLogId,
            Amount = snapshot.Amount,
            DeductedOn = snapshot.DeductedOn,
            Notes = snapshot.Notes,
            IsForDeletion = snapshot.IsForDeletion,
            SpendingSource = new SpendingSourceVM
            {
                Id = snapshot.SpendingSourceId,
                Name = source?.Name ?? string.Empty,
                SpendingSourceType = source?.SpendingSourceType ?? SpendingSourceType.Checking
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
}
