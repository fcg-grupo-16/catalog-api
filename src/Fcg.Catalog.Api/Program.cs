using Fcg.Catalog.Api.Middlewares;
using Fcg.Catalog.Application.Validators;
using Fcg.Catalog.Infrastructure.Extensions;
using Fcg.Catalog.Infrastructure.Seed;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MongoDB.Driver;
using RabbitMQ.Client;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services));


    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();



    // Conexão RabbitMQ ÚNICA e reutilizada pelo health check. Antes o AddRabbitMQ abria uma conexão
    // nova a cada readiness sem fechá-la (leak que saturava o broker). Lazy<Task<IConnection>>
    // garante que a factory é chamada no máximo UMA vez mesmo com checagens concorrentes
    // (thread-safe por padrão: LazyThreadSafetyMode.ExecutionAndPublication) e a mesma Task é
    // compartilhada por todos os waiters. O processo sobe mesmo com o broker fora/lento; o check
    // reporta 503 até a conexão ser estabelecida, e se reconecta sozinha (AutomaticRecoveryEnabled).
    // Respeita RabbitMq:Port (porta dinâmica nos testes; 5672 em compose/k8s).
    var lazyRabbitConnection = new Lazy<Task<IConnection>>(() =>
    {
        var rabbitPort = ushort.TryParse(builder.Configuration["RabbitMq:Port"], out var parsedPort) ? parsedPort : (ushort)5672;
        return new ConnectionFactory
        {
            HostName = builder.Configuration["RabbitMq:Host"] ?? "localhost",
            UserName = builder.Configuration["RabbitMq:Username"] ?? "guest",
            Password = builder.Configuration["RabbitMq:Password"] ?? "guest",
            Port = rabbitPort,
            AutomaticRecoveryEnabled = true
        }.CreateConnectionAsync();
    });

    builder.Services.AddHealthChecks()
        // Reaproveita o IMongoClient singleton do DI (resolvido em runtime) — evita abrir
        // um segundo client/pool de conexões só para o health check.
        .AddMongoDb(sp => sp.GetRequiredService<IMongoClient>(), name: "mongodb", tags: ["ready"])
        .AddRabbitMQ(
            factory: _ => lazyRabbitConnection.Value,
            name: "rabbitmq",
            tags: ["ready"]);

    builder.Services.AddSwaggerExtension();
    builder.Services.AddValidatorsFromAssemblyContaining<CriarJogoValidator>();

    builder.Services.AddMongoDb(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddInfrastructureServices();
    builder.Services.AddMessaging(builder.Configuration);

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "FIAP Cloud Games - Catalog API v1");
            options.DocumentTitle = "FIAP Cloud Games - Catalog API - Documentação";
        });
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false               // nenhum check de dependencia: so processo vivo
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")  // Mongo + RabbitMQ
    });

    // Descarta a conexão do health check ao encerrar a aplicação.
    app.Lifetime.ApplicationStopping.Register(() =>
    {
        if (lazyRabbitConnection.IsValueCreated && lazyRabbitConnection.Value.IsCompletedSuccessfully)
            lazyRabbitConnection.Value.Result.Dispose();
    });

    try
    {
        await DatabaseSeed.SeedAsync(app.Services);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Seed de dados falhou. A aplicação continuará sem dados iniciais.");
    }

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Aplicação encerrada inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
