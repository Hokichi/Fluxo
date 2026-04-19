using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public partial class StartupWizardBudgetAllocationVM : ObservableRecipient,
    IRecipient<StartupWizardSpendingSourcesChangedMessage>
{
    private readonly IUnitOfWork _unitOfWork;
    private decimal _totalBudgetAmount;

    [ObservableProperty] private string _budgetAllocationErrorMessage = string.Empty;
    [ObservableProperty] private int _investAllocationPercentage = 20;
    [ObservableProperty] private bool _isStep5Active;
    [ObservableProperty] private int _needsAllocationPercentage = 50;
    [ObservableProperty] private int _wantsAllocationPercentage = 30;

    public StartupWizardBudgetAllocationVM(
        IUnitOfWork unitOfWork,
        IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _unitOfWork = unitOfWork;
        IsActive = true;
    }

    public bool HasBudgetAllocationError => !string.IsNullOrWhiteSpace(BudgetAllocationErrorMessage);

    public string NeedsAllocationAmountText => BuildAllocationAmountText(NeedsAllocationPercentage);

    public string WantsAllocationAmountText => BuildAllocationAmountText(WantsAllocationPercentage);

    public string InvestAllocationAmountText => BuildAllocationAmountText(InvestAllocationPercentage);

    public void Receive(StartupWizardSpendingSourcesChangedMessage message)
    {
        _totalBudgetAmount = message.Value.TotalPrimaryAmount;
        RaiseAmountProperties();
    }

    public async Task LoadAsync()
    {
        var settings = await _unitOfWork.UserSettings.GetAllAsync();
        var settingsByName = settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);

        NeedsAllocationPercentage = StartupWizardShared.ParsePercentage(settingsByName, UserSettingNames.NeedsThreshold, 50m);
        WantsAllocationPercentage = StartupWizardShared.ParsePercentage(settingsByName, UserSettingNames.WantsThreshold, 30m);
        InvestAllocationPercentage = StartupWizardShared.ParsePercentage(settingsByName, UserSettingNames.InvestThreshold, 20m);

        ValidateBudgetAllocation();
        PublishSnapshot();
    }

    public async Task<SettingsOperationResult> SaveAsync()
    {
        var total = NeedsAllocationPercentage + WantsAllocationPercentage + InvestAllocationPercentage;
        if (total != 100)
            return SettingsOperationResult.Failure(
                $"Needs, Wants, and Invest must add up to 100%. Current total: {total}%");

        await StartupWizardShared.UpsertUserSettingAsync(_unitOfWork, UserSettingNames.NeedsThreshold,
            NeedsAllocationPercentage.ToString(CultureInfo.InvariantCulture));
        await StartupWizardShared.UpsertUserSettingAsync(_unitOfWork, UserSettingNames.WantsThreshold,
            WantsAllocationPercentage.ToString(CultureInfo.InvariantCulture));
        await StartupWizardShared.UpsertUserSettingAsync(_unitOfWork, UserSettingNames.InvestThreshold,
            InvestAllocationPercentage.ToString(CultureInfo.InvariantCulture));
        await _unitOfWork.SaveChangesAsync();

        Messenger.Send(new DashboardDataInvalidatedMessage(DashboardDataInvalidationScope.Budget));
        PublishSnapshot();
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
        PublishSnapshot();
    }

    partial void OnWantsAllocationPercentageChanged(int value)
    {
        OnPropertyChanged(nameof(WantsAllocationAmountText));
        ValidateBudgetAllocation();
        PublishSnapshot();
    }

    partial void OnInvestAllocationPercentageChanged(int value)
    {
        OnPropertyChanged(nameof(InvestAllocationAmountText));
        ValidateBudgetAllocation();
        PublishSnapshot();
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
        var amount = decimal.Round(_totalBudgetAmount * percentage / 100m, 2);
        return amount.ToString("N2", CultureInfo.CurrentCulture);
    }

    private void RaiseAmountProperties()
    {
        OnPropertyChanged(nameof(NeedsAllocationAmountText));
        OnPropertyChanged(nameof(WantsAllocationAmountText));
        OnPropertyChanged(nameof(InvestAllocationAmountText));
    }

    private void PublishSnapshot()
    {
        Messenger.Send(new StartupWizardBudgetAllocationChangedMessage(
            new StartupWizardBudgetAllocationChanged(
                NeedsAllocationPercentage,
                WantsAllocationPercentage,
                InvestAllocationPercentage,
                HasBudgetAllocationError)));
    }
}

