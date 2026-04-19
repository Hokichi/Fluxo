namespace Fluxo.ViewModels.Popups.Settings;

public sealed record SettingsCurrencyOptionVM(string Code, string Name, string Symbol)
{
    public string DisplayName => $"{Name} ({Code})";
}
