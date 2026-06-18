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
using Fluxo.ViewModels.Popups.Helpers;
using Fluxo.ViewModels.Shell;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Popups;

public partial class TransferFundsVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly int _sourceAccountId;
    private readonly IAppDataService _appData;

    [ObservableProperty] private decimal _amountText;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string _noteText = string.Empty;
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private AccountVM? _selectedTarget;

    public TransferFundsVM(MainVM mainViewModel, AccountVM source, IAppDataService appData)
    {
        _mainViewModel = mainViewModel;
        _sourceAccountId = source.Id;
        _appData = appData;
        SourceName = source.Name;
        TargetsView = AccountComboBoxViewFactory.CreateGroupedByTypeThenName(
            Targets,
            nameof(AccountVM.TypeDisplayName),
            nameof(AccountVM.AccountType),
            nameof(AccountVM.Name));

        var candidateTargets = _mainViewModel.BudgetPanel.Accounts
            .Where(account => account.Id != source.Id && account.IsEnabled)
            .OrderBy(account => account.AccountType)
            .ThenBy(account => account.Name)
            .ToList();

        foreach (var candidate in candidateTargets)
            Targets.Add(candidate);

        SelectedTarget = Targets.FirstOrDefault();
    }

    public string PopupTitle => "Transfer Funds";

    public string SourceName { get; }

    public ObservableCollection<AccountVM> Targets { get; } = [];
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
            var source = await _appData.GetAccountByIdAsync(_sourceAccountId);
            if (source is null)
                return TransferFundsResult.Failure("Unable to load the source account.");

            var target = await _appData.GetAccountByIdAsync(input.TargetAccountId);
            if (target is null)
                return TransferFundsResult.Failure("Please choose a valid destination account.");

            var expenseTag = await ResolveTransferTagAsync();
            if (expenseTag is null)
                return TransferFundsResult.Failure("Add at least one expense tag before creating a transfer.");

            var expense = new Expense
            {
                Name = $"Transfer to {target.Name}",
                Amount = input.Amount,
                ExpenseCategory = ExpenseCategory.Savings,
                AccountId = source.Id,
                ExpenseTagId = expenseTag.Id
            };

            var expenseLog = new ExpenseLog
            {
                Expense = expense,
                Amount = input.Amount,
                DeductedOn = input.Date,
                Notes = BuildExpenseNote(target.Name, input.Note),
                IsForDeletion = false,
                AccountId = source.Id
            };

            var incomeLog = new IncomeLog
            {
                Name = BuildIncomeName(source.Name),
                Amount = input.Amount,
                AddedOn = input.Date,
                Notes = input.Note,
                AccountId = target.Id
            };

            await _appData.AddExpenseAsync(expense);
            await _appData.AddExpenseLogAsync(expenseLog);
            await _appData.AddIncomeLogAsync(incomeLog);

            ApplyExpenseToAccount(source, input.Amount);
            ApplyIncomeToAccount(target, input.Amount);

            _appData.UpdateAccount(source);
            _appData.UpdateAccount(target);

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
                            expense.ExpenseCategory,
                            source.Id,
                            expenseTag.Id,
                            expenseLog.DeductedOn,
                            expenseLog.Notes,
                            expenseLog.IsForDeletion)),
                        new AddIncomeLogMemoryAction(new IncomeLogMemorySnapshot(
                            incomeLog.Id,
                            target.Id,
                            incomeLog.Name,
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

    private static string BuildIncomeName(string sourceName)
    {
        return $"Transfer from {sourceName}";
    }

    private static void ApplyExpenseToAccount(Account account, decimal amount)
    {
        if (account.AccountType is AccountType.Credit or AccountType.BNPL)
        {
            account.SpentAmount += amount;
            return;
        }

        account.Balance -= amount;
    }

    private static void ApplyIncomeToAccount(Account account, decimal amount)
    {
        if (account.AccountType is AccountType.Credit or AccountType.BNPL)
        {
            account.SpentAmount = Math.Max(0m, account.SpentAmount - amount);
            return;
        }

        account.Balance += amount;
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
        int TargetAccountId,
        DateTime Date,
        string Note);
}
