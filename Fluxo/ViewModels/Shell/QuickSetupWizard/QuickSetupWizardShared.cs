using System.Collections.ObjectModel;
using System.Globalization;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;

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
