using System.Diagnostics.Metrics;
using System.Text.Json;
using Fcg.Catalog.Application.Interfaces;
using Fcg.Catalog.Infrastructure.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Fcg.Catalog.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Meter Meter = new("Fcg.Catalog.Cache");
    private static readonly Counter<long> CacheAccess = Meter.CreateCounter<long>("fcg_cache_access_total");

    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly RedisSettings _settings;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IDistributedCache cache,
        IConnectionMultiplexer multiplexer,
        IOptions<RedisSettings> settings,
        ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _multiplexer = multiplexer;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var bytes = await _cache.GetAsync(key, ct);
            var hit = bytes is not null && bytes.Length > 0;
            if (!hit)
            {
                CacheAccess.Add(1, new KeyValuePair<string, object?>("result", "miss"));
                return null;
            }

            var value = JsonSerializer.Deserialize<T>(bytes, JsonOptions);
            CacheAccess.Add(1, new KeyValuePair<string, object?>("result", value is not null ? "hit" : "miss"));
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao ler a chave {CacheKey}; seguindo sem cache.", key);
            CacheAccess.Add(1, new KeyValuePair<string, object?>("result", "miss"));
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class
    {
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromSeconds(_settings.DefaultTtlSeconds)
            };

            await _cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions), options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gravar a chave {CacheKey}; ignorando.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _cache.RemoveAsync(key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao remover a chave {CacheKey}; ignorando.", key);
        }
    }

    public async Task<long> GetGenerationAsync(string group, CancellationToken ct = default)
    {
        try
        {
            var value = await Database().StringGetAsync(GenerationKey(group));
            if (!value.HasValue || value.IsNull)
                return 0;

            var text = value.ToString();
            return long.TryParse(text, out var generation) ? generation : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao ler a geração do grupo {CacheGroup}; assumindo 0.", group);
            return 0;
        }
    }

    public async Task InvalidateGroupAsync(string group, CancellationToken ct = default)
    {
        try
        {
            await Database().StringIncrementAsync(GenerationKey(group));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao invalidar o grupo {CacheGroup}; entradas obsoletas expiram por TTL.", group);
        }
    }

    private IDatabase Database() => _multiplexer.GetDatabase();

    private string GenerationKey(string group) => $"{_settings.InstanceName}gen:{group}";
}
