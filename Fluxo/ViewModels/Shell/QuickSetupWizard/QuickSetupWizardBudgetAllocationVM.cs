using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public partial class QuickSetupWizardBudgetAllocationVM : ObservableRecipient,
    IRecipient<QuickSetupWizardSpendingSourcesChangedMessage>
{
    private readonly IAppDataService _appData;
    private bool _isLoadingBudgetAllocation;
    private decimal _totalBudgetAmount;

    [ObservableProperty] private decimal _allocationLimit;
    [ObservableProperty] private AllocationPeriod _allocationPeriod = AllocationPeriod.Monthly;
    [ObservableProperty] private string _budgetAllocationErrorMessage = string.Empty;
    [ObservableProperty] private int _investAllocationPercentage = 20;
    [ObservableProperty] private bool _isStep5Active;
    [ObservableProperty] private int _needsAllocationPercentage = 50;
    [ObservableProperty] private OverspendPolicy _overspendPolicy = OverspendPolicy.Ignore;
    [ObservableProperty] private RolloverPolicy _rolloverPolicy = RolloverPolicy.None;
    [ObservableProperty] private int _wantsAllocationPercentage = 30;

    public QuickSetupWizardBudgetAllocationVM(
        IAppDataService appData,
        IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _appData = appData;
        IsActive = true;
    }

    public bool HasBudgetAllocationError => !string.IsNullOrWhiteSpace(BudgetAllocationErrorMessage);

    public string NeedsAllocationAmountText => BuildAllocationAmountText(NeedsAllocationPercentage);

    public string WantsAllocationAmountText => BuildAllocationAmountText(WantsAllocationPercentage);

    public string InvestAllocationAmountText => BuildAllocationAmountText(InvestAllocationPercentage);

    public void Receive(QuickSetupWizardSpendingSourcesChangedMessage message)
    {
        _totalBudgetAmount = message.Value.TotalPrimaryAmount;
        RaiseAmountProperties();
    }

    public async Task LoadAsync()
    {
        var allocation = await _appData.GetBudgetAllocationAsync();

        _isLoadingBudgetAllocation = true;
        try
        {
            NeedsAllocationPercentage = allocation.NeedsThreshold;
            WantsAllocationPercentage = allocation.WantsThreshold;
            InvestAllocationPercentage = allocation.InvestThreshold;
            AllocationLimit = allocation.AllocationLimit;
            AllocationPeriod = allocation.AllocationPeriod;
            RolloverPolicy = allocation.RolloverPolicy;
            OverspendPolicy = allocation.OverspendPolicy;
        }
        finally
        {
            _isLoadingBudgetAllocation = false;
        }

        ValidateBudgetAllocation();
        PublishSnapshot();
    }

    public async Task<SettingsOperationResult> SaveAsync()
    {
        var applyResult = await ApplyAsync(_appData);
        if (!applyResult.IsSuccess)
            return applyResult;

        await _appData.SaveChangesAsync();

        Messenger.Send(new DashboardDataInvalidatedMessage(DashboardDataInvalidationScope.Budget));
        PublishSnapshot();
        return SettingsOperationResult.Success();
    }

    public async Task<SettingsOperationResult> ApplyAsync(IAppDataService appData)
    {
        ValidateBudgetAllocation();
        if (HasBudgetAllocationError)
            return SettingsOperationResult.Failure(BudgetAllocationErrorMessage);

        var allocation = await appData.GetBudgetAllocationAsync();
        allocation.NeedsThreshold = NeedsAllocationPercentage;
        allocation.WantsThreshold = WantsAllocationPercentage;
        allocation.InvestThreshold = InvestAllocationPercentage;
        allocation.AllocationLimit = AllocationLimit;
        allocation.AllocationPeriod = AllocationPeriod;
        allocation.RolloverPolicy = RolloverPolicy;
        allocation.OverspendPolicy = OverspendPolicy;
        appData.UpdateBudgetAllocation(allocation);
        return SettingsOperationResult.Success();
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

    partial void OnNeedsAllocationPercentageChanged(int value)
    {
        OnPropertyChanged(nameof(NeedsAllocationAmountText));
        ValidateBudgetAllocation();
        if (!_isLoadingBudgetAllocation)
            PublishSnapshot();
    }

    partial void OnWantsAllocationPercentageChanged(int value)
    {
        OnPropertyChanged(nameof(WantsAllocationAmountText));
        ValidateBudgetAllocation();
        if (!_isLoadingBudgetAllocation)
            PublishSnapshot();
    }

    partial void OnInvestAllocationPercentageChanged(int value)
    {
        OnPropertyChanged(nameof(InvestAllocationAmountText));
        ValidateBudgetAllocation();
        if (!_isLoadingBudgetAllocation)
            PublishSnapshot();
    }

    partial void OnAllocationLimitChanged(decimal value)
    {
        RaiseAmountProperties();
    }

    private void ValidateBudgetAllocation()
    {
        var total = NeedsAllocationPercentage + WantsAllocationPercentage + InvestAllocationPercentage;
        BudgetAllocationErrorMessage = total == 100
            ? string.Empty
            : $"Needs, Wants, and Invest must add up to 100%. Current total: {total}%";
        OnPropertyChanged(nameof(HasBudgetAllocationError));
    }

    private string BuildAllocationAmountText(int percentage)
    {
        var amount = decimal.Round(ResolveAllocationBaseAmount() * percentage / 100m, 2);
        return amount.ToString("N2", CultureInfo.CurrentCulture);
    }

    private decimal ResolveAllocationBaseAmount()
    {
        return AllocationLimit > 0m ? AllocationLimit : _totalBudgetAmount;
    }

    private void RaiseAmountProperties()
    {
        OnPropertyChanged(nameof(NeedsAllocationAmountText));
        OnPropertyChanged(nameof(WantsAllocationAmountText));
        OnPropertyChanged(nameof(InvestAllocationAmountText));
    }

    private void PublishSnapshot()
    {
        Messenger.Send(new QuickSetupWizardBudgetAllocationChangedMessage(
            new QuickSetupWizardBudgetAllocationChanged(
                NeedsAllocationPercentage,
                WantsAllocationPercentage,
                InvestAllocationPercentage,
                HasBudgetAllocationError)));
    }
}
