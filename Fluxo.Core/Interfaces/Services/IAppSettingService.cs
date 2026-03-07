namespace Fluxo.Core.Interfaces.Services;

public interface IAppSettingService
{
    // ── Typed accessors (avoids magic strings throughout the codebase) ─────────

    Task<string> GetCurrencyAsync();

    Task SetCurrencyAsync(string isoCode);

    Task<int> GetNotificationLeadDaysAsync();

    Task SetNotificationLeadDaysAsync(int days);

    /// <summary>
    /// Day of month used as the default date for new income / expense entries
    /// when the user doesn't manually choose a date (default = 1).
    /// </summary>
    Task<int> GetDefaultEntryDayAsync();

    Task SetDefaultEntryDayAsync(int day);

    Task<string> GetThemeAsync();

    Task SetThemeAsync(string theme);

    Task<string?> GetAiApiKeyAsync();

    Task SetAiApiKeyAsync(string key);

    Task<string?> GetAiProviderAsync();

    Task SetAiProviderAsync(string provider);

    // ── Raw key-value access for extensibility ────────────────────────────────
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    /// <summary>Returns the default entry date for the current month.</summary>
    Task<DateTime> GetDefaultEntryDateAsync();
}