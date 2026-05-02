namespace Fluxo.Resources.Resources.Messages;

[Flags]
public enum SettingsTabKey
{
    Budget = 1 << 0,
    Sources = 1 << 1,
    FixedExpenses = 1 << 2,
    Goals = 1 << 3,
    Tags = 1 << 4,
    Personalization = 1 << 5
}

public readonly record struct SettingsTabLoaded(
    SettingsOperationCorrelation Operation,
    SettingsTabKey TabKey,
    bool IsSuccess,
    string? ErrorMessage);

public sealed class SettingsTabLoadedMessage(SettingsTabLoaded value)
    : ValueChangedMessage<SettingsTabLoaded>(value);

