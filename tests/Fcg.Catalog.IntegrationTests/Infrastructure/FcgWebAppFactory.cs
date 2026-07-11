using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MongoDb;
using Testcontainers.RabbitMq;

namespace Fcg.Catalog.IntegrationTests.Infrastructure;

/// <summary>
/// Sobe a Catalog API contra um MongoDB (replica set rs0, exigido pelo outbox) e um
/// RabbitMQ reais via Testcontainers.
/// </summary>
public sealed class FcgWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtSecret = "IntegrationTests_HmacSha256_Secret_Key_With_At_Least_32_Chars!";
    public const string JwtIssuer = "FiapCloudGames";
    public const string JwtAudience = "FiapCloudGames";

    private const string RabbitUsername = "guest";
    private const string RabbitPassword = "guest";

    private readonly string _databaseName = $"catalogdb_it_{Guid.NewGuid():N}";
    private string? _mongoConnectionString;

    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7")
        .WithReplicaSet("rs0")
        .Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:3-management")
        .WithPortBinding(5672, 5672)
        .WithUsername(RabbitUsername)
        .WithPassword(RabbitPassword)
        .Build();

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();
        await _rabbit.StartAsync();
        _mongoConnectionString = _mongo.GetConnectionString();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("MongoDbSettings:ConnectionString", _mongoConnectionString ?? _mongo.GetConnectionString());
        builder.UseSetting("MongoDbSettings:DatabaseName", _databaseName);
        builder.UseSetting("RabbitMq:Host", "localhost");
        builder.UseSetting("RabbitMq:Username", RabbitUsername);
        builder.UseSetting("RabbitMq:Password", RabbitPassword);
        builder.UseSetting("JwtSettings:SecretKey", JwtSecret);
        builder.UseSetting("JwtSettings:Issuer", JwtIssuer);
        builder.UseSetting("JwtSettings:Audience", JwtAudience);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _rabbit.DisposeAsync();
        await _mongo.DisposeAsync();
        await DisposeAsync();
    }
}
