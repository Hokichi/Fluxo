using System.Collections.ObjectModel;
using System.Globalization;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.ViewModels.Shell.StartupWizard;

internal static class StartupWizardShared
{
    public const string DefaultCurrencyCode = "USD";
    public const int TotalSteps = 10;

    public static string ResolveUsername(string? username)
    {
        return string.IsNullOrWhiteSpace(username) ? "User" : username.Trim();
    }

    public static IReadOnlyList<SettingsCurrencyOptionVM> BuildCurrencyOptions()
    {
        return
        [
            new SettingsCurrencyOptionVM("USD", "US Dollar", "$"),
            new SettingsCurrencyOptionVM("EUR", "Euro", "EUR"),
            new SettingsCurrencyOptionVM("GBP", "British Pound", "GBP"),
            new SettingsCurrencyOptionVM("JPY", "Japanese Yen", "JPY"),
            new SettingsCurrencyOptionVM("THB", "Thai Baht", "THB"),
            new SettingsCurrencyOptionVM("AUD", "Australian Dollar", "A$"),
            new SettingsCurrencyOptionVM("CAD", "Canadian Dollar", "C$"),
            new SettingsCurrencyOptionVM("SGD", "Singapore Dollar", "S$"),
            new SettingsCurrencyOptionVM("VND", "Vietnamese Dong", "VND"),
            new SettingsCurrencyOptionVM("INR", "Indian Rupee", "INR")
        ];
    }

    public static string ParseCurrencyCode(
        IReadOnlyDictionary<string, string> settings,
        IEnumerable<SettingsCurrencyOptionVM> options,
        string name,
        string defaultValue)
    {
        var code = ParseString(settings, name, defaultValue).ToUpperInvariant();
        return options.Any(option => string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase))
            ? code
            : defaultValue;
    }

    public static string ParseString(IReadOnlyDictionary<string, string> settings, string name, string defaultValue)
    {
        if (!settings.TryGetValue(name, out var value))
            return defaultValue;

        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0 ? defaultValue : trimmed;
    }

    public static bool ParseBool(IReadOnlyDictionary<string, string> settings, string name, bool defaultValue)
    {
        return settings.TryGetValue(name, out var value) && bool.TryParse(value, out var parsedValue)
            ? parsedValue
            : defaultValue;
    }

    public static int ParsePercentage(IReadOnlyDictionary<string, string> settings, string name, decimal defaultValue)
    {
        if (!settings.TryGetValue(name, out var value) ||
            !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue))
            return (int)defaultValue;

        return (int)Math.Round(parsedValue, MidpointRounding.AwayFromZero);
    }

    public static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
            collection.Add(item);
    }

    public static async Task UpsertUserSettingAsync(IUnitOfWork unitOfWork, string name, string? value)
    {
        var existingSetting = await unitOfWork.UserSettings.GetByNameAsync(name);

        if (value is null)
        {
            if (existingSetting is not null)
                unitOfWork.UserSettings.Remove(existingSetting);

            return;
        }

        if (existingSetting is null)
        {
            await unitOfWork.UserSettings.AddAsync(new UserSettings { Name = name, Value = value });
            return;
        }

        existingSetting.Value = value;
        unitOfWork.UserSettings.Update(existingSetting);
    }
}
