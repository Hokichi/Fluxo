namespace Fluxo.Resources.Resources.Messages;

[Flags]
public enum SettingsDataChangedScope
{
    None = 0,
    Accounts = 1 << 0,
    FixedExpenses = 1 << 1,
    SavingGoals = 1 << 2,
    Tags = 1 << 3,
    UserSettings = 1 << 4,
    All = Accounts | FixedExpenses | SavingGoals | Tags | UserSettings
}

public sealed class SettingsDataChangedMessage(SettingsDataChangedScope value)
    : ValueChangedMessage<SettingsDataChangedScope>(value);

