using System.Collections.ObjectModel;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Messages;
using EntityReadUnitOfWork = Fluxo.Core.Interfaces.IViewModelReadUnitOfWork<
    Fluxo.ViewModels.Entities.ExpenseVM,
    Fluxo.ViewModels.Entities.ExpenseLogVM,
    Fluxo.ViewModels.Entities.IncomeLogVM,
    Fluxo.ViewModels.Entities.ExpenseTagVM,
    Fluxo.ViewModels.Entities.SavingGoalVM,
    Fluxo.ViewModels.Entities.SpendingSourceVM>;

namespace Fluxo.ViewModels.Shell;

public partial class MainVM
{
    private readonly Func<EntityReadUnitOfWork> _readUnitOfWorkFactory;
    private bool _isApplyingExpenseDetailRefresh;

    private void HandleExpenseDetailUpdatedMessage(ExpenseDetailUpdatedMessage message)
    {
        _ = HandleExpenseDetailUpdatedAsync(message.Value);
    }

    private async Task HandleExpenseDetailUpdatedAsync(ExpenseDetailUpdate update)
    {
        if (!update.HasChanges)
            return;

        using var readUnitOfWork = _readUnitOfWorkFactory();

        var updatedExpenseLog = await readUnitOfWork.ExpenseLogs.GetByIdAsync(update.ExpenseLogId);
        if (updatedExpenseLog is null || updatedExpenseLog.Expense is null || updatedExpenseLog.SpendingSource is null)
            return;

        IReadOnlyList<ExpenseTagVM>? refreshedTags = null;
        Dictionary<int, SpendingSourceVM>? refreshedSources = null;

        if (update.AffectsTagOrdering)
            refreshedTags = (await readUnitOfWork.ExpenseTags.GetTagsByCountDescendingAsync())
                .Select(result => result.Tag)
                .ToList();

        if (update.AffectsSpendingSourceState)
        {
            refreshedSources = [];

            foreach (var sourceId in GetAffectedSpendingSourceIds(update.PreviousState, updatedExpenseLog))
            {
                var source = await readUnitOfWork.SpendingSources.GetByIdAsync(sourceId);
                if (source is not null)
                    refreshedSources[sourceId] = source;
            }
        }

        RunBatchedExpenseRefresh(() =>
        {
            UpdateTrackedExpense(updatedExpenseLog.Expense);
            UpdateTrackedExpenseLog(updatedExpenseLog);

            if (update.AffectsAllTimeTotals)
                ApplyAllTimeExpenseDelta(update.PreviousState, updatedExpenseLog);

            if (refreshedSources is not null)
                UpdateTrackedSpendingSources(refreshedSources);

            if (update.AffectsVisibleMoneyOut)
                RefreshVisibleMoneyOutMetrics(GetAffectedSpendingSourceIds(update.PreviousState, updatedExpenseLog));

            if (refreshedTags is not null)
                LoadTags(refreshedTags);
        });

        RefreshDashboardMetrics();
        RefreshNotifications();
    }

    private void RunBatchedExpenseRefresh(Action action)
    {
        _isApplyingExpenseDetailRefresh = true;

        try
        {
            action();
        }
        finally
        {
            _isApplyingExpenseDetailRefresh = false;
        }
    }

    private void UpdateTrackedExpense(ExpenseVM updatedExpense)
    {
        var index = _expenses.FindIndex(expense => expense.Id == updatedExpense.Id);
        if (index >= 0)
        {
            _expenses[index] = updatedExpense;
            return;
        }

        _expenses.Add(updatedExpense);
    }

    private void UpdateTrackedExpenseLog(ExpenseLogVM updatedExpenseLog)
    {
        var existingExpenseLog = FindExpenseLogInCollections(updatedExpenseLog.Id);
        var trackedExpenseLog = existingExpenseLog ?? updatedExpenseLog;
        var currentCollection = FindExpenseLogCollection(updatedExpenseLog.Id);

        if (existingExpenseLog is not null)
            CopyExpenseLog(existingExpenseLog, updatedExpenseLog);

        var isVisibleInCurrentView = IsExpenseLogVisibleInCurrentView(updatedExpenseLog.DeductedOn);
        if (!isVisibleInCurrentView)
        {
            if (currentCollection is not null)
                currentCollection.Remove(trackedExpenseLog);

            return;
        }

        var targetCollection =
            GetExpenseCollection(updatedExpenseLog.Expense?.ExpenseCategory ?? ExpenseCategory.Needs);

        if (currentCollection is not null && !ReferenceEquals(currentCollection, targetCollection))
            currentCollection.Remove(trackedExpenseLog);

        if (!targetCollection.Any(log => log.Id == trackedExpenseLog.Id))
            targetCollection.Add(trackedExpenseLog);

        SortExpenseLogs(targetCollection);
    }

    private void UpdateTrackedSpendingSources(IReadOnlyDictionary<int, SpendingSourceVM> refreshedSources)
    {
        foreach (var refreshedSource in refreshedSources.Values)
        {
            var trackedSource = SpendingSources.FirstOrDefault(source => source.Id == refreshedSource.Id);
            if (trackedSource is null)
                continue;

            trackedSource.Name = refreshedSource.Name;
            trackedSource.SpendingSourceType = refreshedSource.SpendingSourceType;
            trackedSource.AccountLimit = refreshedSource.AccountLimit;
            trackedSource.Balance = refreshedSource.Balance;
            trackedSource.DueDate = refreshedSource.DueDate;
            trackedSource.InterestRate = refreshedSource.InterestRate;
            trackedSource.ShowOnUI = refreshedSource.ShowOnUI;
            trackedSource.IsEnabled = refreshedSource.IsEnabled;
            trackedSource.SpentAmount = refreshedSource.SpentAmount;
        }
    }

    private void RefreshVisibleMoneyOutMetrics(IEnumerable<int> sourceIds)
    {
        var affectedSourceIds = sourceIds.Distinct().ToList();
        if (affectedSourceIds.Count == 0)
            return;

        var moneyOutBySourceId = GetAllExpenseLogs()
            .Where(log => !log.IsForDeletion && affectedSourceIds.Contains(log.SpendingSource.Id))
            .GroupBy(log => log.SpendingSource.Id)
            .ToDictionary(group => group.Key, group => group.Sum(log => log.Amount));

        foreach (var sourceId in affectedSourceIds)
        {
            var spendingSource = SpendingSources.FirstOrDefault(source => source.Id == sourceId);
            if (spendingSource is null)
                continue;

            spendingSource.MoneyOut = moneyOutBySourceId.GetValueOrDefault(sourceId);
        }
    }

    private void ApplyAllTimeExpenseDelta(ExpenseDetailSnapshot previousState, ExpenseLogVM updatedExpenseLog)
    {
        AddToAllTimeSpent(previousState.Category, -previousState.Amount);
        AddToAllTimeSpent(updatedExpenseLog.Expense?.ExpenseCategory ?? ExpenseCategory.Needs,
            updatedExpenseLog.Amount);
    }

    private void AddToAllTimeSpent(ExpenseCategory category, decimal amountDelta)
    {
        switch (category)
        {
            case ExpenseCategory.Needs:
                _allTimeNeedsSpent += amountDelta;
                break;

            case ExpenseCategory.Wants:
                _allTimeWantsSpent += amountDelta;
                break;

            case ExpenseCategory.Savings:
                _allTimeInvestSpent += amountDelta;
                break;
        }
    }

    private bool IsExpenseLogVisibleInCurrentView(DateTime expenseDate)
    {
        if (SelectedMainContentViewMode == MainContentViewMode.AllTime)
            return true;

        var selectedDate = SelectedDay?.Date ?? DateTime.Today;
        var targetDate = expenseDate.Date;

        return SelectedMainContentViewMode switch
        {
            MainContentViewMode.Daily => targetDate == selectedDate.Date,
            MainContentViewMode.Weekly => targetDate >= selectedDate.Date && targetDate < selectedDate.Date.AddDays(7),
            MainContentViewMode.Monthly => targetDate.Year == selectedDate.Year &&
                                           targetDate.Month == selectedDate.Month,
            _ => false
        };
    }

    private ObservableCollection<ExpenseLogVM> GetExpenseCollection(ExpenseCategory category)
    {
        return category switch
        {
            ExpenseCategory.Wants => _wantsSource,
            ExpenseCategory.Savings => _investSource,
            _ => _needsSource
        };
    }

    private ExpenseLogVM? FindExpenseLogInCollections(int expenseLogId)
    {
        return _needsSource
            .Concat(_wantsSource)
            .Concat(_investSource)
            .FirstOrDefault(log => log.Id == expenseLogId);
    }

    private ObservableCollection<ExpenseLogVM>? FindExpenseLogCollection(int expenseLogId)
    {
        if (_needsSource.Any(log => log.Id == expenseLogId))
            return _needsSource;

        if (_wantsSource.Any(log => log.Id == expenseLogId))
            return _wantsSource;

        if (_investSource.Any(log => log.Id == expenseLogId))
            return _investSource;

        return null;
    }

    private static IEnumerable<int> GetAffectedSpendingSourceIds(ExpenseDetailSnapshot previousState,
        ExpenseLogVM updatedExpenseLog)
    {
        return new[] { previousState.SpendingSourceId, updatedExpenseLog.SpendingSource.Id }
            .Where(id => id > 0)
            .Distinct();
    }

    private static void CopyExpenseLog(ExpenseLogVM target, ExpenseLogVM source)
    {
        target.Amount = source.Amount;
        target.DeductedOn = source.DeductedOn;
        target.Expense = source.Expense;
        target.IsForDeletion = source.IsForDeletion;
        target.Notes = source.Notes;
        target.SpendingSource = source.SpendingSource;
    }

    private static void SortExpenseLogs(ObservableCollection<ExpenseLogVM> expenseLogs)
    {
        var orderedExpenseLogs = expenseLogs
            .OrderByDescending(log => log.DeductedOn)
            .ToList();

        expenseLogs.Clear();

        foreach (var expenseLog in orderedExpenseLogs)
            expenseLogs.Add(expenseLog);
    }
}