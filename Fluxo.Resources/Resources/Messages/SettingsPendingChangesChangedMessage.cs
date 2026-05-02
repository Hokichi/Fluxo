namespace Fluxo.Resources.Resources.Messages;

public readonly record struct SettingsPendingChangesChanged(
    SettingsTabKey TabKey,
    bool HasPendingChanges);

public sealed class SettingsPendingChangesChangedMessage(SettingsPendingChangesChanged value)
    : ValueChangedMessage<SettingsPendingChangesChanged>(value);
