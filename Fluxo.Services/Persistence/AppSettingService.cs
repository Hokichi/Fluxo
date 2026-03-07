using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class AppSettingService : IAppSettingService
{
    private readonly IAppSettingRepository _repo;

    public AppSettingService(IAppSettingRepository repo)
    {
        _repo = repo;
    }

    public async Task<string> GetCurrencyAsync()
    {
        return await _repo.GetAsync(AppSetting.Keys.Currency) ?? "USD";
    }

    public Task SetCurrencyAsync(string isoCode)
    {
        return _repo.SetAsync(AppSetting.Keys.Currency, isoCode.ToUpperInvariant());
    }

    public async Task<int> GetNotificationLeadDaysAsync()
    {
        return int.TryParse(await _repo.GetAsync(AppSetting.Keys.NotificationLeadDays), out var d) ? d : 3;
    }

    public Task SetNotificationLeadDaysAsync(int days)
    {
        return _repo.SetAsync(AppSetting.Keys.NotificationLeadDays, days.ToString());
    }

    public async Task<int> GetDefaultEntryDayAsync()
    {
        return int.TryParse(await _repo.GetAsync(AppSetting.Keys.DefaultEntryDay), out var d) ? d : 1;
    }

    public Task SetDefaultEntryDayAsync(int day)
    {
        if (day is < 1 or > 28)
            throw new ArgumentOutOfRangeException(nameof(day), "Default entry day must be between 1 and 28.");
        return _repo.SetAsync(AppSetting.Keys.DefaultEntryDay, day.ToString());
    }

    public async Task<string> GetThemeAsync()
    {
        return await _repo.GetAsync(AppSetting.Keys.Theme) ?? "system";
    }

    public Task SetThemeAsync(string theme)
    {
        return _repo.SetAsync(AppSetting.Keys.Theme, theme);
    }

    public Task<string?> GetAiApiKeyAsync()
    {
        return _repo.GetAsync(AppSetting.Keys.AiApiKey);
    }

    public Task SetAiApiKeyAsync(string key)
    {
        return _repo.SetAsync(AppSetting.Keys.AiApiKey, key);
    }

    public Task<string?> GetAiProviderAsync()
    {
        return _repo.GetAsync(AppSetting.Keys.AiProvider);
    }

    public Task SetAiProviderAsync(string provider)
    {
        return _repo.SetAsync(AppSetting.Keys.AiProvider, provider);
    }

    public Task<string?> GetAsync(string key)
    {
        return _repo.GetAsync(key);
    }

    public Task SetAsync(string key, string value)
    {
        return _repo.SetAsync(key, value);
    }

    public async Task<DateTime> GetDefaultEntryDateAsync()
    {
        var day = await GetDefaultEntryDayAsync();
        var today = DateTime.Today;
        var clampedDay = Math.Min(day, DateTime.DaysInMonth(today.Year, today.Month));
        return new DateTime(today.Year, today.Month, clampedDay);
    }
}