using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.ViewModels.Popups;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public partial class StartupWizardSavingGoalsVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessenger _messenger;

    [ObservableProperty] private bool _isStep4Active;

    public StartupWizardSavingGoalsVM(
        MainVM mainViewModel,
        IUnitOfWork unitOfWork,
        IMessenger? messenger = null)
    {
        _mainViewModel = mainViewModel;
        _unitOfWork = unitOfWork;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    public ObservableCollection<StartupWizardSavingGoalItemVM> SavingGoals { get; } = [];

    public AddSavingGoalVM CreateAddViewModel()
    {
        return new AddSavingGoalVM(_mainViewModel, _unitOfWork);
    }

    public async Task<AddSavingGoalVM> CreateEditViewModelAsync(int id)
    {
        var goal = await _unitOfWork.SavingGoals.GetByIdAsync(id);
        if (goal is null)
            return CreateAddViewModel();

        return new AddSavingGoalVM(_mainViewModel, _unitOfWork)
        {
            EditingId = goal.Id,
            NameText = goal.Name,
            TargetAmountText = goal.TargetAmount.ToString("N2", CultureInfo.InvariantCulture),
            CurrentAmountText = goal.CurrentAmount.ToString("N2", CultureInfo.InvariantCulture),
            EndDate = goal.SavingEndDate
        };
    }

    public async Task DeleteAsync(int id)
    {
        var goal = await _unitOfWork.SavingGoals.GetByIdAsync(id);
        if (goal is not null)
        {
            _unitOfWork.SavingGoals.Remove(goal);
            await _unitOfWork.SaveChangesAsync();
            _messenger.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.SavingGoals));
        }

        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        StartupWizardShared.ReplaceCollection(SavingGoals, (await _unitOfWork.SavingGoals.GetAllAsync())
            .OrderBy(goal => goal.SavingEndDate)
            .ThenBy(goal => goal.Name)
            .Select(goal => new StartupWizardSavingGoalItemVM(goal)));

        PublishSnapshot();
    }

    private void PublishSnapshot()
    {
        _messenger.Send(new StartupWizardSavingGoalsChangedMessage(
            new StartupWizardSavingGoalsChanged(SavingGoals.Count)));
    }
}
