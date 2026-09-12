using Fcg.Catalog.Application.Interfaces;

namespace Fcg.Catalog.Infrastructure.Caching;

public sealed class NoOpCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        => Task.FromResult<T?>(null);

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class
        => Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;

    public Task<long> GetGenerationAsync(string group, CancellationToken ct = default) => Task.FromResult(0L);

    public Task InvalidateGroupAsync(string group, CancellationToken ct = default) => Task.CompletedTask;
}
