using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.ViewModels.Shell;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Popups.Settings;

public partial class SettingsBudgetTabVM : ObservableObject
{
    private readonly Func<decimal> _totalBudgetAmountProvider;
    private readonly IMessenger _messenger;
    private readonly IAppDataService _appData;
    private bool _suppressPendingStatePublish;
    private BudgetAllocationSnapshot _savedBudgetAllocation = new(
        50,
        30,
        20,
        0m,
        AllocationPeriod.Monthly,
        RolloverPolicy.None,
        OverspendPolicy.Ignore);

    [ObservableProperty] private decimal _allocationLimit;
    [ObservableProperty] private AllocationPeriod _allocationPeriod = AllocationPeriod.Monthly;
    [ObservableProperty] private string _budgetAllocationErrorMessage = string.Empty;
    [ObservableProperty] private int _investAllocationPercentage;
    [ObservableProperty] private int _needsAllocationPercentage;
    [ObservableProperty] private OverspendPolicy _overspendPolicy = OverspendPolicy.Ignore;
    [ObservableProperty] private RolloverPolicy _rolloverPolicy = RolloverPolicy.None;
    [ObservableProperty] private int _wantsAllocationPercentage;

    public SettingsBudgetTabVM(MainVM mainViewModel, IAppDataService appData, IMessenger? messenger = null)
        : this(() => mainViewModel.BudgetPanel.TotalIncomeAmount, appData, messenger)
    {
    }

    public SettingsBudgetTabVM(Func<decimal> totalBudgetAmountProvider, IAppDataService appData,
        IMessenger? messenger = null)
    {
        _totalBudgetAmountProvider = totalBudgetAmountProvider;
        _appData = appData;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    public decimal TotalBudgetAmount => _totalBudgetAmountProvider();
    public bool HasBudgetAllocationError => !string.IsNullOrWhiteSpace(BudgetAllocationErrorMessage);
    public bool HasSpendingSources { get; private set; }

    public bool HasPendingChanges =>
        NeedsAllocationPercentage != _savedBudgetAllocation.Needs ||
        WantsAllocationPercentage != _savedBudgetAllocation.Wants ||
        InvestAllocationPercentage != _savedBudgetAllocation.Invest ||
        AllocationLimit != _savedBudgetAllocation.AllocationLimit ||
        AllocationPeriod != _savedBudgetAllocation.AllocationPeriod ||
        RolloverPolicy != _savedBudgetAllocation.RolloverPolicy ||
        OverspendPolicy != _savedBudgetAllocation.OverspendPolicy;

    public bool CanSaveConfiguration => !HasBudgetAllocationError;

    public string ConfigurationErrorMessage => BudgetAllocationErrorMessage;

    public string NeedsAllocationAmountText => BuildAllocationAmountText(NeedsAllocationPercentage);
    public string WantsAllocationAmountText => BuildAllocationAmountText(WantsAllocationPercentage);
    public string InvestAllocationAmountText => BuildAllocationAmountText(InvestAllocationPercentage);

    public async Task LoadAsync()
    {
        var allocation = await _appData.GetBudgetAllocationAsync();
        _suppressPendingStatePublish = true;
        try
        {
            NeedsAllocationPercentage = allocation.NeedsThreshold;
            WantsAllocationPercentage = allocation.WantsThreshold;
            InvestAllocationPercentage = allocation.InvestThreshold;
            AllocationLimit = allocation.AllocationLimit;
            AllocationPeriod = allocation.AllocationPeriod;
            RolloverPolicy = allocation.RolloverPolicy;
            OverspendPolicy = allocation.OverspendPolicy;

            _savedBudgetAllocation = new BudgetAllocationSnapshot(
                NeedsAllocationPercentage,
                WantsAllocationPercentage,
                InvestAllocationPercentage,
                AllocationLimit,
                AllocationPeriod,
                RolloverPolicy,
                OverspendPolicy);

            HasSpendingSources = (await _appData.GetSpendingSourcesAsync()).Count > 0;
            ValidateBudgetAllocation();
            OnPropertyChanged(nameof(HasSpendingSources));
            OnPropertyChanged(nameof(TotalBudgetAmount));
            OnPropertyChanged(nameof(NeedsAllocationAmountText));
            OnPropertyChanged(nameof(WantsAllocationAmountText));
            OnPropertyChanged(nameof(InvestAllocationAmountText));
        }
        finally
        {
            _suppressPendingStatePublish = false;
        }

        PublishPendingState();
    }

    public async Task<(SettingsOperationResult Result, List<ILogMemoryAction> Actions)> BuildApplyChangesAsync()
    {
        ValidateBudgetAllocation();
        if (HasBudgetAllocationError)
            return (SettingsOperationResult.Failure(BudgetAllocationErrorMessage), []);

        var allocation = await _appData.GetBudgetAllocationAsync();
        allocation.NeedsThreshold = NeedsAllocationPercentage;
        allocation.WantsThreshold = WantsAllocationPercentage;
        allocation.InvestThreshold = InvestAllocationPercentage;
        allocation.AllocationLimit = AllocationLimit;
        allocation.AllocationPeriod = AllocationPeriod;
        allocation.RolloverPolicy = RolloverPolicy;
        allocation.OverspendPolicy = OverspendPolicy;
        _appData.UpdateBudgetAllocation(allocation);

        return (SettingsOperationResult.Success(), []);
    }

    public void CommitSavedState()
    {
        _savedBudgetAllocation = new BudgetAllocationSnapshot(
            NeedsAllocationPercentage,
            WantsAllocationPercentage,
            InvestAllocationPercentage,
            AllocationLimit,
            AllocationPeriod,
            RolloverPolicy,
            OverspendPolicy);
        PublishPendingState();
    }

    public void OpenAddSpendingSource()
    {
        _messenger.Send(new SettingsDialogRequestedMessage(
            new SettingsDialogRequest(SettingsDialogRequestType.AddSpendingSource)));
    }

    public void RevertChanges()
    {
        NeedsAllocationPercentage = _savedBudgetAllocation.Needs;
        WantsAllocationPercentage = _savedBudgetAllocation.Wants;
        InvestAllocationPercentage = _savedBudgetAllocation.Invest;
        AllocationLimit = _savedBudgetAllocation.AllocationLimit;
        AllocationPeriod = _savedBudgetAllocation.AllocationPeriod;
        RolloverPolicy = _savedBudgetAllocation.RolloverPolicy;
        OverspendPolicy = _savedBudgetAllocation.OverspendPolicy;
        ValidateBudgetAllocation();
        PublishPendingState();
    }

    public void IncrementAllocation(BudgetAllocationSegment segment, int delta)
    {
        switch (segment)
        {
            case BudgetAllocationSegment.Needs:
                NeedsAllocationPercentage = Math.Clamp(NeedsAllocationPercentage + delta, 0, 100);
                break;

            case BudgetAllocationSegment.Wants:
                WantsAllocationPercentage = Math.Clamp(WantsAllocationPercentage + delta, 0, 100);
                break;

            case BudgetAllocationSegment.Invest:
                InvestAllocationPercentage = Math.Clamp(InvestAllocationPercentage + delta, 0, 100);
                break;
        }
    }

    public void SetAllocation(BudgetAllocationSegment segment, double value)
    {
        var roundedValue = Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 100);
        switch (segment)
        {
            case BudgetAllocationSegment.Needs:
                NeedsAllocationPercentage = roundedValue;
                break;

            case BudgetAllocationSegment.Wants:
                WantsAllocationPercentage = roundedValue;
                break;

            case BudgetAllocationSegment.Invest:
                InvestAllocationPercentage = roundedValue;
                break;
        }
    }

    partial void OnNeedsAllocationPercentageChanged(int value)
    {
        OnAllocationChanged();
    }

    partial void OnWantsAllocationPercentageChanged(int value)
    {
        OnAllocationChanged();
    }

    partial void OnInvestAllocationPercentageChanged(int value)
    {
        OnAllocationChanged();
    }

    partial void OnAllocationLimitChanged(decimal value)
    {
        RaiseAmountProperties();
        PublishPendingState();
    }

    partial void OnAllocationPeriodChanged(AllocationPeriod value)
    {
        PublishPendingState();
    }

    partial void OnRolloverPolicyChanged(RolloverPolicy value)
    {
        PublishPendingState();
    }

    partial void OnOverspendPolicyChanged(OverspendPolicy value)
    {
        PublishPendingState();
    }

    private string BuildAllocationAmountText(int percentage)
    {
        var allocatedAmount = decimal.Round(ResolveAllocationBaseAmount() * percentage / 100m, 2);
        return allocatedAmount.ToString("N2", CultureInfo.CurrentCulture);
    }

    private void OnAllocationChanged()
    {
        ValidateBudgetAllocation();
        RaiseAmountProperties();
        PublishPendingState();
    }

    private decimal ResolveAllocationBaseAmount()
    {
        return AllocationLimit > 0m ? AllocationLimit : TotalBudgetAmount;
    }

    private void RaiseAmountProperties()
    {
        OnPropertyChanged(nameof(NeedsAllocationAmountText));
        OnPropertyChanged(nameof(WantsAllocationAmountText));
        OnPropertyChanged(nameof(InvestAllocationAmountText));
    }

    private void ValidateBudgetAllocation()
    {
        var total = NeedsAllocationPercentage + WantsAllocationPercentage + InvestAllocationPercentage;
        BudgetAllocationErrorMessage = total == 100
            ? string.Empty
            : $"Needs, Wants, and Invest must add up to 100%. Current total: {total}%";
        OnPropertyChanged(nameof(HasBudgetAllocationError));
    }

    private void PublishPendingState()
    {
        if (_suppressPendingStatePublish)
            return;

        _messenger.Send(new SettingsPendingChangesChangedMessage(
            new SettingsPendingChangesChanged(SettingsTabKey.Budget, HasPendingChanges)));
    }
}

