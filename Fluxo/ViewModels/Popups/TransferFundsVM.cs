using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;

namespace Fluxo.ViewModels.Popups;

public partial class TransferFundsVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly int _sourceSpendingSourceId;
    private readonly IUnitOfWork _uow;

    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string _noteText = string.Empty;
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private SpendingSourceVM? _selectedTarget;

    public TransferFundsVM(MainVM mainViewModel, SpendingSourceVM source, IUnitOfWork uow)
    {
        _mainViewModel = mainViewModel;
        _sourceSpendingSourceId = source.Id;
        _uow = uow;
        SourceName = source.Name;

        var candidateTargets = _mainViewModel.BudgetPanel.SpendingSources
            .Where(spendingSource => spendingSource.Id != source.Id)
            .OrderByDescending(spendingSource => spendingSource.ShowOnUI)
            .ThenBy(spendingSource => spendingSource.Name)
            .ToList();

        foreach (var candidate in candidateTargets)
            Targets.Add(candidate);

        SelectedTarget = Targets.FirstOrDefault();
    }

    public string PopupTitle => "Transfer Funds";

    public string SourceName { get; }

    public ObservableCollection<SpendingSourceVM> Targets { get; } = [];

    public async Task<TransferFundsResult> SaveAsync()
    {
        if (IsSaving)
            return TransferFundsResult.Failure("This transfer is already being saved.");

        if (!TryBuildInput(out var input, out var validationMessage))
            return TransferFundsResult.Failure(validationMessage);

        IsSaving = true;

        try
        {
            var source = await _uow.SpendingSources.GetByIdAsync(_sourceSpendingSourceId);
            if (source is null)
                return TransferFundsResult.Failure("Unable to load the source spending source.");

            var target = await _uow.SpendingSources.GetByIdAsync(input.TargetSpendingSourceId);
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

            await _uow.Expenses.AddAsync(expense);
            await _uow.ExpenseLogs.AddAsync(expenseLog);
            await _uow.IncomeLogs.AddAsync(incomeLog);

            ApplyExpenseToSpendingSource(source, input.Amount);
            ApplyIncomeToSpendingSource(target, input.Amount);

            _uow.SpendingSources.Update(source);
            _uow.SpendingSources.Update(target);

            await _uow.SaveChangesAsync();

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
            return TransferFundsResult.Failure($"Unable to save this transfer.\n\n{exception.Message}");
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
        var expenseTags = await _uow.ExpenseTags.GetAllAsync();
        return expenseTags
            .OrderByDescending(tag => string.Equals(tag.Name, "Transfer", StringComparison.OrdinalIgnoreCase))
            .ThenBy(tag => tag.Name)
            .FirstOrDefault();
    }

    private bool TryBuildInput(out TransferFundsInput input, out string validationMessage)
    {
        input = default;
        validationMessage = string.Empty;

        if (!TryParseAmount(out var amount))
        {
            validationMessage = "Please enter a valid transfer amount greater than zero.";
            return false;
        }

        if (SelectedTarget is null)
        {
            validationMessage = "Please choose where the funds should go.";
            return false;
        }

        input = new TransferFundsInput(amount, SelectedTarget.Id, SelectedDate.Date, NoteText.Trim());
        return true;
    }

    private bool TryParseAmount(out decimal amount)
    {
        amount = 0m;
        var normalizedAmount = AmountText
            .Trim()
            .Replace(CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol, string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(normalizedAmount))
            return false;

        if (!decimal.TryParse(normalizedAmount, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                CultureInfo.CurrentCulture, out amount) &&
            !decimal.TryParse(normalizedAmount, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                CultureInfo.InvariantCulture, out amount))
            return false;

        return amount > 0m;
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
