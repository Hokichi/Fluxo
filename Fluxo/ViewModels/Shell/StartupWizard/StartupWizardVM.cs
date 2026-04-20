using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Data.Context;
using Fluxo.Resources.Messages;
using Fluxo.ViewModels.Popups.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.ExceptionServices;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public partial class StartupWizardVM : ObservableRecipient,
    IRecipient<StartupWizardSpendingSourcesChangedMessage>,
    IRecipient<StartupWizardBudgetAllocationChangedMessage>
{
    private readonly MainVM _mainViewModel;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDataOperationScopeFactory _dataOperationScopeFactory;
    private IDataOperationScope? _stagedScope;
    private Func<Task>? _stagedCommitAsync;
    private Func<Task>? _stagedRollbackAsync;

    [ObservableProperty] private int _currentStepIndex;
    [ObservableProperty] private bool _hasSpendingSources;

    public StartupWizardVM(
        MainVM mainViewModel,
        IUnitOfWork unitOfWork,
        IDataOperationScopeFactory dataOperationScopeFactory,
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
        _dataOperationScopeFactory = dataOperationScopeFactory;

        GreetingPage = greetingPage;
        NamePage = namePage;
        MiddlePage = middlePage;
        LoadingPage = loadingPage;
        FinalPage = finalPage;

        IsActive = true;
    }

    public StartupWizardGreetingPageVM GreetingPage { get; }

    public StartupWizardNamePageVM NamePage { get; }

    public StartupWizardMiddlePageVM MiddlePage { get; }

    public StartupWizardLoadingPageVM LoadingPage { get; }

    public StartupWizardFinalPageVM FinalPage { get; }

    public int TotalSteps => StartupWizardShared.TotalSteps;

    public int CurrentStep => CurrentStepIndex + 1;

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
        OnPropertyChanged(nameof(CurrentStep));

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

    public Task<StartupWizardLoadingOutcome> ExecuteLoadingFlowAsync(
        Func<Task<bool>>? tryStageAsyncOverride,
        Func<Task<bool>> confirmRetryCycleAsync,
        Func<TimeSpan, Task>? delayAsync = null)
    {
        return StartupWizardLoadingCoordinator.RunAsync(
            tryStageAsyncOverride ?? TryStageDraftAsync,
            confirmRetryCycleAsync,
            delayAsync ?? (duration => Task.Delay(duration)));
    }

    public async Task<SettingsOperationResult> CompleteAsync()
    {
        Exception? capturedException = null;

        try
        {
            if (_stagedCommitAsync is not null)
                await _stagedCommitAsync();

            await SaveIsFirstRunAsync(false);

            if (!_mainViewModel.IsInitialized)
                await _mainViewModel.Initialize();
            else
                await _mainViewModel.ReloadCurrentDataAsync();
        }
        catch (Exception exception)
        {
            capturedException = exception;
        }

        try
        {
            await ClearStagedAsync(rollbackStagedChanges: false);
        }
        catch (Exception exception)
        {
            capturedException ??= exception;
        }

        return capturedException is null
            ? SettingsOperationResult.Success()
            : SettingsOperationResult.Failure(CreateStartupWizardErrorMessage("finish setup", capturedException));
    }

    public async Task<SettingsOperationResult> DismissAsync()
    {
        try
        {
            await ClearStagedAsync();
            await SaveIsFirstRunAsync(false);

            if (!_mainViewModel.IsInitialized)
                await _mainViewModel.Initialize();
            else
                await _mainViewModel.ReloadCurrentDataAsync();
        }
        catch (Exception exception)
        {
            return SettingsOperationResult.Failure(
                CreateStartupWizardErrorMessage("close the startup wizard", exception));
        }

        return SettingsOperationResult.Success();
    }

    private async Task<SettingsOperationResult> PersistCurrentStepAsync()
    {
        return await Task.FromResult(SettingsOperationResult.Success());
    }

    private async Task<bool> TryStageDraftAsync()
    {
        IDataOperationScope? scope = null;
        IDbContextTransaction? transaction = null;

        try
        {
            await ClearStagedAsync();

            scope = await _dataOperationScopeFactory.CreateAsync();
            var dbContext = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();
            transaction = await dbContext.Database.BeginTransactionAsync();
            var stagedTransaction = transaction ??
                throw new InvalidOperationException("Unable to begin startup wizard staging transaction.");

            var stagedUnitOfWork = scope.UnitOfWork;
            await NamePage.ApplyAsync(stagedUnitOfWork);
            await MiddlePage.BudgetAllocation.ApplyAsync(stagedUnitOfWork);
            await MiddlePage.Notification.ApplyAsync(stagedUnitOfWork);
            await MiddlePage.SpendingSources.ApplyAsync(stagedUnitOfWork);
            await MiddlePage.FixedExpenses.ApplyAsync(
                stagedUnitOfWork,
                MiddlePage.SpendingSources.LastPersistedIdMap);
            await MiddlePage.SavingGoals.ApplyAsync(stagedUnitOfWork);
            await stagedUnitOfWork.SaveChangesAsync();

            _stagedScope = scope;
            _stagedCommitAsync = async () =>
            {
                await ExecuteTransactionActionAndDisposeAsync(stagedTransaction, tx => tx.CommitAsync());
            };
            _stagedRollbackAsync = async () =>
            {
                await ExecuteTransactionActionAndDisposeAsync(stagedTransaction, tx => tx.RollbackAsync());
            };

            scope = null;
            transaction = null;
            return true;
        }
        catch
        {
            await DisposeQuietlyAsync(transaction);
            await DisposeQuietlyAsync(scope);
            await ClearStagedQuietlyAsync();
            return false;
        }
    }

    private static async Task DisposeQuietlyAsync(IAsyncDisposable? disposable)
    {
        if (disposable is null)
            return;

        try
        {
            await disposable.DisposeAsync();
        }
        catch
        {
            // Staging failures should surface as a retryable loading attempt.
        }
    }

    private async Task ClearStagedQuietlyAsync()
    {
        try
        {
            await ClearStagedAsync();
        }
        catch
        {
            _stagedScope = null;
            _stagedCommitAsync = null;
            _stagedRollbackAsync = null;
        }
    }

    private static async Task ExecuteTransactionActionAndDisposeAsync(
        IDbContextTransaction transaction,
        Func<IDbContextTransaction, Task> actionAsync)
    {
        ExceptionDispatchInfo? capturedException = null;

        try
        {
            await actionAsync(transaction);
        }
        catch (Exception exception)
        {
            capturedException = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            await transaction.DisposeAsync();
        }
        catch (Exception exception)
        {
            capturedException ??= ExceptionDispatchInfo.Capture(exception);
        }

        capturedException?.Throw();
    }

    private async Task ClearStagedAsync(bool rollbackStagedChanges = true)
    {
        var stagedScope = _stagedScope;
        var stagedRollbackAsync = _stagedRollbackAsync;
        ExceptionDispatchInfo? capturedException = null;

        _stagedScope = null;
        _stagedCommitAsync = null;
        _stagedRollbackAsync = null;

        try
        {
            if (rollbackStagedChanges && stagedRollbackAsync is not null)
                await stagedRollbackAsync();
        }
        catch (Exception exception)
        {
            capturedException = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            if (stagedScope is not null)
                await stagedScope.DisposeAsync();
        }
        catch (Exception exception)
        {
            capturedException ??= ExceptionDispatchInfo.Capture(exception);
        }

        capturedException?.Throw();
    }

    private static string CreateStartupWizardErrorMessage(string action, Exception exception)
    {
        return $"Unable to {action}.\n\n{exception.Message}";
    }

    private async Task SaveIsFirstRunAsync(bool isFirstRun)
    {
        await StartupWizardShared.UpsertUserSettingAsync(_unitOfWork, UserSettingNames.IsFirstRun, isFirstRun.ToString());
        await _unitOfWork.SaveChangesAsync();
    }
}
