using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.ViewModels.Helpers;
using Fluxo.ViewModels.Popups;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public partial class StartupWizardSpendingSourcesVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessenger _messenger;

    [ObservableProperty] private bool _isStep2Active;

    public StartupWizardSpendingSourcesVM(
        MainVM mainViewModel,
        IUnitOfWork unitOfWork,
        IMessenger? messenger = null)
    {
        _mainViewModel = mainViewModel;
        _unitOfWork = unitOfWork;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    public ObservableCollection<StartupWizardSpendingSourceItemVM> SpendingSources { get; } = [];

    public bool HasSpendingSources => SpendingSources.Count > 0;

    public decimal TotalBudgetAmount => SpendingSources.Sum(source => source.PrimaryAmount);

    public AddSpendingSourceVM CreateAddViewModel()
    {
        return new AddSpendingSourceVM(_mainViewModel, _unitOfWork);
    }

    public async Task<AddSpendingSourceVM> CreateEditViewModelAsync(int id)
    {
        var source = await _unitOfWork.SpendingSources.GetByIdAsync(id);
        if (source is null)
            return CreateAddViewModel();

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
            vm.MonthlyDueDateText = MonthlyDueDateHelper.Normalize(source.MonthlyDueDate)?.ToString(CultureInfo.InvariantCulture) ??
                                    string.Empty;
            vm.SelectedDeductSource = source.DeductSource;
        }
        else
        {
            vm.PrimaryAmountText = source.Balance.ToString("N2", CultureInfo.InvariantCulture);
        }

        if (source.SpendingSourceType == SpendingSourceType.Saving && source.InterestRate.HasValue)
            vm.ApyText = source.InterestRate.Value.ToString("N2", CultureInfo.InvariantCulture);

        return vm;
    }

    public async Task DeleteAsync(int id)
    {
        var source = await _unitOfWork.SpendingSources.GetByIdAsync(id);
        if (source is not null)
        {
            _unitOfWork.SpendingSources.Remove(source);
            await _unitOfWork.SaveChangesAsync();
            _messenger.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));
        }

        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        StartupWizardShared.ReplaceCollection(SpendingSources, (await _unitOfWork.SpendingSources.GetAllAsync())
            .OrderBy(source => source.Name)
            .Select(source => new StartupWizardSpendingSourceItemVM(source)));

        OnPropertyChanged(nameof(HasSpendingSources));
        OnPropertyChanged(nameof(TotalBudgetAmount));
        PublishSnapshot();
    }

    private void PublishSnapshot()
    {
        _messenger.Send(new StartupWizardSpendingSourcesChangedMessage(
            new StartupWizardSpendingSourcesChanged(
                SpendingSources.Count,
                HasSpendingSources,
                TotalBudgetAmount)));
    }
}
