namespace Fcg.Catalog.Application.Interfaces;

/// <summary>
/// Cache distribuído da aplicação. Abstrai o Redis do restante do sistema.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<long> GetGenerationAsync(string group, CancellationToken ct = default);
    Task InvalidateGroupAsync(string group, CancellationToken ct = default);
}
