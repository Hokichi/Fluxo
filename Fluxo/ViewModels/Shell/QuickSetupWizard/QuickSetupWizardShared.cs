using System.Collections.ObjectModel;
using System.Globalization;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

internal static class QuickSetupWizardShared
{
    public const int TotalSteps = 10;

    public static string ResolveUsername(string? username)
    {
        return string.IsNullOrWhiteSpace(username) ? "User" : username.Trim();
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

    public static async Task UpsertUserSettingAsync(IAppDataService appData, string name, string? value)
    {
        var existingSetting = await appData.GetUserSettingByNameAsync(name);

        if (value is null)
        {
            if (existingSetting is not null)
                appData.RemoveUserSetting(existingSetting);

            return;
        }

        if (existingSetting is null)
        {
            await appData.AddUserSettingAsync(new UserSettings { Name = name, Value = value });
            return;
        }

        existingSetting.Value = value;
        appData.UpdateUserSetting(existingSetting);
    }
}

