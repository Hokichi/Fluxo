namespace Fluxo.Resources.Resources.Messages;

public readonly record struct SettingsSettingChange(
    string Name,
    object? PreviousValue,
    object? CurrentValue);

public readonly record struct SettingsUsernameChange(
    string? PreviousValue,
    string? CurrentValue);

public readonly record struct SettingsApplyContribution(
    SettingsOperationCorrelation Operation,
    SettingsTabKey TabKey,
    bool IsSuccess,
    string? ErrorMessage,
    IReadOnlyList<SettingsSettingChange> SettingChanges,
    IReadOnlyList<ILogMemoryAction> MemoryActions,
    SettingsUsernameChange? UsernameChange);

public sealed class SettingsApplyContributionMessage(SettingsApplyContribution value)
    : ValueChangedMessage<SettingsApplyContribution>(value);
