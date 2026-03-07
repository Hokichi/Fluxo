namespace Fluxo.Core.Interfaces.Repositories;

public interface IAppSettingRepository
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    Task<IReadOnlyDictionary<string, string>> GetAllAsync();
}