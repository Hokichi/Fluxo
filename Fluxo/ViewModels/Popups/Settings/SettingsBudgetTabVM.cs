using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
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
    private BudgetAllocationSnapshot _savedBudgetAllocation = new(50, 30, 20);

    [ObservableProperty] private string _budgetAllocationErrorMessage = string.Empty;
    [ObservableProperty] private int _investAllocationPercentage;
    [ObservableProperty] private int _needsAllocationPercentage;
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
        InvestAllocationPercentage != _savedBudgetAllocation.Invest;

    public bool CanSaveConfiguration => !HasBudgetAllocationError;

    public string ConfigurationErrorMessage => BudgetAllocationErrorMessage;

    public string NeedsAllocationAmountText => BuildAllocationAmountText(NeedsAllocationPercentage);
    public string WantsAllocationAmountText => BuildAllocationAmountText(WantsAllocationPercentage);
    public string InvestAllocationAmountText => BuildAllocationAmountText(InvestAllocationPercentage);

    public async Task LoadAsync()
    {
        var settingsByName = await SettingsShared.GetSettingsDictionaryAsync(_appData);
        NeedsAllocationPercentage = SettingsShared.ParsePercentage(settingsByName, UserSettingNames.NeedsThreshold, 50m);
        WantsAllocationPercentage = SettingsShared.ParsePercentage(settingsByName, UserSettingNames.WantsThreshold, 30m);
        InvestAllocationPercentage = SettingsShared.ParsePercentage(settingsByName, UserSettingNames.InvestThreshold, 20m);

        _savedBudgetAllocation = new BudgetAllocationSnapshot(
            NeedsAllocationPercentage,
            WantsAllocationPercentage,
            InvestAllocationPercentage);

        HasSpendingSources = (await _appData.GetSpendingSourcesAsync()).Count > 0;
        ValidateBudgetAllocation();
        OnPropertyChanged(nameof(HasSpendingSources));
        OnPropertyChanged(nameof(TotalBudgetAmount));
        OnPropertyChanged(nameof(NeedsAllocationAmountText));
        OnPropertyChanged(nameof(WantsAllocationAmountText));
        OnPropertyChanged(nameof(InvestAllocationAmountText));
        PublishPendingState();
    }

    public async Task<(SettingsOperationResult Result, List<ILogMemoryAction> Actions)> BuildApplyChangesAsync()
    {
        ValidateBudgetAllocation();
        if (HasBudgetAllocationError)
            return (SettingsOperationResult.Failure(BudgetAllocationErrorMessage), []);

        var actions = new List<ILogMemoryAction>();
        await SettingsShared.UpdateUserSettingAsync(_appData, UserSettingNames.NeedsThreshold,
            NeedsAllocationPercentage.ToString(CultureInfo.InvariantCulture), actions);
        await SettingsShared.UpdateUserSettingAsync(_appData, UserSettingNames.WantsThreshold,
            WantsAllocationPercentage.ToString(CultureInfo.InvariantCulture), actions);
        await SettingsShared.UpdateUserSettingAsync(_appData, UserSettingNames.InvestThreshold,
            InvestAllocationPercentage.ToString(CultureInfo.InvariantCulture), actions);

        return (SettingsOperationResult.Success(), actions);
    }

    public void CommitSavedState()
    {
        _savedBudgetAllocation = new BudgetAllocationSnapshot(
            NeedsAllocationPercentage,
            WantsAllocationPercentage,
            InvestAllocationPercentage);
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

    private string BuildAllocationAmountText(int percentage)
    {
        var allocatedAmount = decimal.Round(TotalBudgetAmount * percentage / 100m, 2);
        return allocatedAmount.ToString("N2", CultureInfo.CurrentCulture);
    }

    private void OnAllocationChanged()
    {
        ValidateBudgetAllocation();
        OnPropertyChanged(nameof(NeedsAllocationAmountText));
        OnPropertyChanged(nameof(WantsAllocationAmountText));
        OnPropertyChanged(nameof(InvestAllocationAmountText));
        PublishPendingState();
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
        _messenger.Send(new SettingsPendingChangesChangedMessage(
            new SettingsPendingChangesChanged(SettingsTabKey.Budget, HasPendingChanges)));
    }
}

