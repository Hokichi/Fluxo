using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.ViewModels.Popups.Settings;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public partial class StartupWizardVM : ObservableRecipient,
    IRecipient<StartupWizardSpendingSourcesChangedMessage>,
    IRecipient<StartupWizardBudgetAllocationChangedMessage>
{
    private readonly MainVM _mainViewModel;
    private readonly IUnitOfWork _unitOfWork;

    [ObservableProperty] private int _currentStepIndex;
    [ObservableProperty] private bool _hasSpendingSources;

    public StartupWizardVM(
        MainVM mainViewModel,
        IUnitOfWork unitOfWork,
        StartupWizardGreetingPageVM greetingPage,
        StartupWizardNamePageVM namePage,
        StartupWizardMiddlePageVM middlePage,
        StartupWizardLoadingPageVM loadingPage,
        StartupWizardFinalPageVM finalPage,
        IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _mainViewModel = mainViewModel;
        _unitOfWork = unitOfWork;

        GreetingPage = greetingPage;
        NamePage = namePage;
        MiddlePage = middlePage;
        LoadingPage = loadingPage;
        FinalPage = finalPage;

        for (var i = 0; i < TotalSteps; i++)
            StepDots.Add(new StartupWizardStepDotVM(i, i == 0));

        IsActive = true;
    }

    public StartupWizardGreetingPageVM GreetingPage { get; }

    public StartupWizardNamePageVM NamePage { get; }

    public StartupWizardMiddlePageVM MiddlePage { get; }

    public StartupWizardLoadingPageVM LoadingPage { get; }

    public StartupWizardFinalPageVM FinalPage { get; }

    public ObservableCollection<StartupWizardStepDotVM> StepDots { get; } = [];

    public int TotalSteps => StartupWizardShared.TotalSteps;

    public bool IsGreetingStep => CurrentStepIndex == 0;

    public bool IsNameStep => CurrentStepIndex == 1;

    public bool IsMiddleStep => CurrentStepIndex is >= 2 and <= 7;

    public bool IsLoadingStep => CurrentStepIndex == 8;

    public bool IsFinalStep => CurrentStepIndex == TotalSteps - 1;

    public bool IsStep2Active => CurrentStepIndex == 2;

    public bool IsNextEnabled => !(CurrentStepIndex == 5 && MiddlePage.BudgetAllocation.HasBudgetAllocationError);

    partial void OnCurrentStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsGreetingStep));
        OnPropertyChanged(nameof(IsNameStep));
        OnPropertyChanged(nameof(IsMiddleStep));
        OnPropertyChanged(nameof(IsLoadingStep));
        OnPropertyChanged(nameof(IsFinalStep));
        OnPropertyChanged(nameof(IsStep2Active));
        OnPropertyChanged(nameof(IsNextEnabled));

        foreach (var dot in StepDots)
            dot.IsActive = dot.StepIndex == value;

        if (value is >= 2 and <= 7)
            MiddlePage.SetCurrentStepIndex(value);
    }

    public void Receive(StartupWizardSpendingSourcesChangedMessage message)
    {
        HasSpendingSources = message.Value.HasAny;
    }

    public void Receive(StartupWizardBudgetAllocationChangedMessage message)
    {
        OnPropertyChanged(nameof(IsNextEnabled));
    }

    public async Task LoadAsync()
    {
        await NamePage.LoadAsync();
        await MiddlePage.LoadAsync();
    }

    public void GoBack()
    {
        if (CurrentStepIndex <= 0)
            return;

        if (IsFinalStep)
        {
            CurrentStepIndex = 6;
            return;
        }

        if (CurrentStepIndex == 5 && !HasSpendingSources)
        {
            CurrentStepIndex = 2;
            return;
        }

        CurrentStepIndex--;
    }

    public void NavigateToStep(int stepIndex)
    {
        if (stepIndex >= 0 && stepIndex < TotalSteps && stepIndex != CurrentStepIndex)
            CurrentStepIndex = stepIndex;
    }

    public async Task<SettingsOperationResult> GoNextAsync()
    {
        var result = await PersistCurrentStepAsync();
        if (!result.IsSuccess)
            return result;

        if (CurrentStepIndex < TotalSteps - 1)
            CurrentStepIndex++;

        return SettingsOperationResult.Success();
    }

    public async Task InitializeMainViewModelAsync()
    {
        await _mainViewModel.Initialize();
    }

    public async Task<SettingsOperationResult> CompleteAsync()
    {
        await SaveIsFirstRunAsync(false);
        return SettingsOperationResult.Success();
    }

    public async Task<SettingsOperationResult> DismissAsync()
    {
        var result = await PersistCurrentStepAsync();
        if (!result.IsSuccess)
            return result;

        await SaveIsFirstRunAsync(false);

        if (!_mainViewModel.IsInitialized)
            await _mainViewModel.Initialize();
        else
            await _mainViewModel.ReloadCurrentDataAsync();

        return SettingsOperationResult.Success();
    }

    private async Task<SettingsOperationResult> PersistCurrentStepAsync()
    {
        return CurrentStepIndex switch
        {
            1 => await NamePage.SaveAsync(),
            5 => await MiddlePage.BudgetAllocation.SaveAsync(),
            6 => await MiddlePage.Notification.SaveAsync(),
            _ => SettingsOperationResult.Success()
        };
    }

    private async Task SaveIsFirstRunAsync(bool isFirstRun)
    {
        await StartupWizardShared.UpsertUserSettingAsync(_unitOfWork, UserSettingNames.IsFirstRun, isFirstRun.ToString());
        await _unitOfWork.SaveChangesAsync();
    }
}
