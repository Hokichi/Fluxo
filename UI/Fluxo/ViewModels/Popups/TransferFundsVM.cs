using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.Services.Logging;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Helpers;
using Fluxo.ViewModels.Shell;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Popups;

public partial class TransferFundsVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly int _sourceSpendingSourceId;
    private readonly IAppDataService _appData;

    [ObservableProperty] private decimal _amountText;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string _noteText = string.Empty;
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private SpendingSourceVM? _selectedTarget;

    public TransferFundsVM(MainVM mainViewModel, SpendingSourceVM source, IAppDataService appData)
    {
        _mainViewModel = mainViewModel;
        _sourceSpendingSourceId = source.Id;
        _appData = appData;
        SourceName = source.Name;
        TargetsView = SpendingSourceComboBoxViewFactory.CreateGroupedByTypeThenName(
            Targets,
            nameof(SpendingSourceVM.TypeDisplayName),
            nameof(SpendingSourceVM.SpendingSourceType),
            nameof(SpendingSourceVM.Name));

        var candidateTargets = _mainViewModel.BudgetPanel.SpendingSources
            .Where(spendingSource => spendingSource.Id != source.Id && spendingSource.IsEnabled)
            .OrderBy(spendingSource => spendingSource.SpendingSourceType)
            .ThenBy(spendingSource => spendingSource.Name)
            .ToList();

        foreach (var candidate in candidateTargets)
            Targets.Add(candidate);

        SelectedTarget = Targets.FirstOrDefault();
    }

    public string PopupTitle => "Transfer Funds";

    public string SourceName { get; }

    public ObservableCollection<SpendingSourceVM> Targets { get; } = [];
    public ICollectionView TargetsView { get; }

    public async Task<TransferFundsResult> SaveAsync()
    {
        if (IsSaving)
            return TransferFundsResult.Failure("This transfer is already being saved.");

        if (!TryBuildInput(out var input, out var validationMessage))
            return TransferFundsResult.Failure(validationMessage);

        IsSaving = true;

        try
        {
            var source = await _appData.GetSpendingSourceByIdAsync(_sourceSpendingSourceId);
            if (source is null)
                return TransferFundsResult.Failure("Unable to load the source spending source.");

            var target = await _appData.GetSpendingSourceByIdAsync(input.TargetSpendingSourceId);
            if (target is null)
                return TransferFundsResult.Failure("Please choose a valid destination account.");

            var expenseTag = await ResolveTransferTagAsync();
            if (expenseTag is null)
                return TransferFundsResult.Failure("Add at least one expense tag before creating a transfer.");

            var expense = new Expense
            {
                Name = $"Transfer to {target.Name}",
                Amount = input.Amount,
                ExpenseKind = ExpenseKind.Manual,
                ExpenseCategory = ExpenseCategory.Savings,
                RecurringDate = input.Date.Day,
                IsActive = false,
                SpendingSourceId = source.Id,
                ExpenseTagId = expenseTag.Id
            };

            var expenseLog = new ExpenseLog
            {
                Expense = expense,
                Amount = input.Amount,
                DeductedOn = input.Date,
                Notes = BuildExpenseNote(target.Name, input.Note),
                IsForDeletion = false,
                SpendingSourceId = source.Id
            };

            var incomeLog = new IncomeLog
            {
                Amount = input.Amount,
                AddedOn = input.Date,
                Notes = BuildIncomeNote(source.Name, input.Note),
                SpendingSourceId = target.Id
            };

            await _appData.AddExpenseAsync(expense);
            await _appData.AddExpenseLogAsync(expenseLog);
            await _appData.AddIncomeLogAsync(incomeLog);

            ApplyExpenseToSpendingSource(source, input.Amount);
            ApplyIncomeToSpendingSource(target, input.Amount);

            _appData.UpdateSpendingSource(source);
            _appData.UpdateSpendingSource(target);

            await _appData.SaveChangesAsync();

            WeakReferenceMessenger.Default.Send(new RecordLogMemoryMessage(
                new CompositeLogMemoryAction(
                    "Transfer funds",
                    [
                        new AddExpenseLogMemoryAction(new ExpenseLogMemorySnapshot(
                            expense.Id,
                            expenseLog.Id,
                            expense.Name,
                            expenseLog.Amount,
                            expense.ExpenseKind,
                            expense.ExpenseCategory,
                            expense.RecurringDate,
                            expense.IsActive,
                            source.Id,
                            expenseTag.Id,
                            expenseLog.DeductedOn,
                            expenseLog.Notes,
                            expenseLog.IsForDeletion)),
                        new AddIncomeLogMemoryAction(new IncomeLogMemorySnapshot(
                            incomeLog.Id,
                            target.Id,
                            incomeLog.Amount,
                            incomeLog.AddedOn,
                            incomeLog.Notes))
                    ])));

            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));

            await _mainViewModel.ReloadCurrentDataAsync();
            return TransferFundsResult.Success();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to save transfer funds transaction.");
            return TransferFundsResult.Failure(FluxoLogManager.CreateFailureMessage("save transfer"));
        }
        finally
        {
            IsSaving = false;
        }
    }

    public bool HasValidEntryToPersistOnClose()
    {
        return TryBuildInput(out _, out _);
    }

    private async Task<ExpenseTag?> ResolveTransferTagAsync()
    {
        var expenseTags = await _appData.GetExpenseTagsAsync();
        return expenseTags
            .OrderByDescending(tag => string.Equals(tag.Name, "Transfer", StringComparison.OrdinalIgnoreCase))
            .ThenBy(tag => tag.Name)
            .FirstOrDefault();
    }

    private bool TryBuildInput(out TransferFundsInput input, out string validationMessage)
    {
        input = default;
        validationMessage = string.Empty;

        if (AmountText <= 0m)
        {
            validationMessage = "Please enter a valid transfer amount greater than zero.";
            return false;
        }

        if (SelectedTarget is null)
        {
            validationMessage = "Please choose where the funds should go.";
            return false;
        }

        input = new TransferFundsInput(AmountText, SelectedTarget.Id, SelectedDate.Date, NoteText.Trim());
        return true;
    }

    private static string BuildExpenseNote(string targetName, string note)
    {
        var transferLine = $"Transfer to {targetName}";
        return string.IsNullOrWhiteSpace(note)
            ? transferLine
            : $"{transferLine}\n{note.Trim()}";
    }

    private static string BuildIncomeNote(string sourceName, string note)
    {
        var transferLine = $"Transfer from {sourceName}";
        return string.IsNullOrWhiteSpace(note)
            ? transferLine
            : $"{transferLine}\n{note.Trim()}";
    }

    private static void ApplyExpenseToSpendingSource(SpendingSource spendingSource, decimal amount)
    {
        if (spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            spendingSource.SpentAmount += amount;
            return;
        }

        spendingSource.Balance -= amount;
    }

    private static void ApplyIncomeToSpendingSource(SpendingSource spendingSource, decimal amount)
    {
        if (spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            spendingSource.SpentAmount = Math.Max(0m, spendingSource.SpentAmount - amount);
            return;
        }

        spendingSource.Balance += amount;
    }

    public readonly record struct TransferFundsResult(bool IsSuccess, string? ErrorMessage)
    {
        public static TransferFundsResult Success()
        {
            return new TransferFundsResult(true, null);
        }

        public static TransferFundsResult Failure(string? errorMessage)
        {
            return new TransferFundsResult(false, errorMessage);
        }
    }

    private readonly record struct TransferFundsInput(
        decimal Amount,
        int TargetSpendingSourceId,
        DateTime Date,
        string Note);
}
