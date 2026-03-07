using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class AppSettingRepository : IAppSettingRepository
{
    private readonly AppDbContext _db;

    public AppSettingRepository(AppDbContext db) => _db = db;

    public async Task<string?> GetAsync(string key)
        => (await _db.AppSettings.FindAsync(key))?.Value;

    public async Task SetAsync(string key, string value)
    {
        var setting = await _db.AppSettings.FindAsync(key);
        if (setting is null)
        {
            await _db.AppSettings.AddAsync(new AppSetting
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
            _db.AppSettings.Update(setting);
        }

        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync()
        => await _db.AppSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
}