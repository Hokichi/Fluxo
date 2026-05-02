namespace Fluxo.Resources.Resources.Messages;

public enum SettingsDialogRequestType
{
    AddSpendingSource,
    AddFixedExpense,
    AddSavingGoal,
    SpendingSourceDetail,
    AddTag,
    FeaturePlaceholder
}

public readonly record struct SettingsDialogRequest(
    SettingsDialogRequestType RequestType,
    object? Payload = null,
    string? Title = null,
    string? Message = null);

public sealed class SettingsDialogRequestedMessage(SettingsDialogRequest value)
    : ValueChangedMessage<SettingsDialogRequest>(value);

public readonly record struct SettingsPopupCloseRequest(bool AllowClose = true);

public sealed class SettingsPopupCloseRequestedMessage(SettingsPopupCloseRequest value)
    : ValueChangedMessage<SettingsPopupCloseRequest>(value);

public enum SettingsMaintenanceRequestType
{
    ResetAllSettings,
    DeleteAllData
}

public readonly record struct SettingsMaintenanceResult(bool IsSuccess, string? ErrorMessage)
{
    public static SettingsMaintenanceResult Success()
    {
        return new SettingsMaintenanceResult(true, null);
    }

    public static SettingsMaintenanceResult Failure(string? errorMessage)
    {
        return new SettingsMaintenanceResult(false, errorMessage);
    }
}

public readonly record struct SettingsMaintenanceRequest(
    SettingsMaintenanceRequestType RequestType,
    bool KeepSettings,
    TaskCompletionSource<SettingsMaintenanceResult> CompletionSource);

public sealed class SettingsMaintenanceRequestedMessage(SettingsMaintenanceRequest value)
    : ValueChangedMessage<SettingsMaintenanceRequest>(value);
