using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.ViewModels.Helpers;
using Fluxo.Resources.Messages;
using Fluxo.ViewModels.Popups;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public partial class StartupWizardFixedExpensesVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessenger _messenger;

    [ObservableProperty] private bool _isStep3Active;

    public StartupWizardFixedExpensesVM(
        MainVM mainViewModel,
        IUnitOfWork unitOfWork,
        IMessenger? messenger = null)
    {
        _mainViewModel = mainViewModel;
        _unitOfWork = unitOfWork;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    public ObservableCollection<StartupWizardFixedExpenseItemVM> FixedExpenses { get; } = [];

    public AddFixedExpenseVM CreateAddViewModel()
    {
        return new AddFixedExpenseVM(_mainViewModel, _unitOfWork);
    }

    public async Task<AddFixedExpenseVM> CreateEditViewModelAsync(int id)
    {
        var expense = await _unitOfWork.Expenses.GetByIdAsync(id);
        if (expense is null)
            return CreateAddViewModel();

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

    public async Task DeleteAsync(int id)
    {
        var expense = await _unitOfWork.Expenses.GetByIdAsync(id);
        if (expense is not null)
        {
            _unitOfWork.Expenses.Remove(expense);
            await _unitOfWork.SaveChangesAsync();
            _messenger.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));
        }

        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        StartupWizardShared.ReplaceCollection(FixedExpenses, (await _unitOfWork.Expenses.GetAllAsync())
            .Where(expense => expense.ExpenseKind == ExpenseKind.Fixed)
            .OrderBy(expense => expense.Name)
            .Select(expense => new StartupWizardFixedExpenseItemVM(expense)));

        PublishSnapshot();
    }

    private void PublishSnapshot()
    {
        _messenger.Send(new StartupWizardFixedExpensesChangedMessage(
            new StartupWizardFixedExpensesChanged(
                FixedExpenses.Count,
                FixedExpenses.Sum(expense => expense.Amount))));
    }
}
