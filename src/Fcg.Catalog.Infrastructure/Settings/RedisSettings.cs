namespace Fcg.Catalog.Infrastructure.Settings;

public sealed class RedisSettings
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; init; } = string.Empty;
    public string InstanceName { get; init; } = "fcg:catalog:";
    public int DefaultTtlSeconds { get; init; } = 120;
    public bool Enabled { get; init; } = true;
}
