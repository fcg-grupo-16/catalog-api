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



    // Conexão RabbitMQ ÚNICA (singleton) para o health check reusar — com auto-recovery. Criada no
    // startup (async, sem sync-over-async); o broker já está up aqui (initContainer/depends_on).
    // Antes o AddRabbitMQ abria uma conexão nova a cada readiness sem fechá-la (leak que saturava o broker).
    // Respeita RabbitMq:Port (porta dinâmica nos testes de integração; 5672 em compose/k8s).
    var rabbitPort = ushort.TryParse(builder.Configuration["RabbitMq:Port"], out var parsedPort) ? parsedPort : (ushort)5672;
    var rabbitConnection = await new ConnectionFactory
    {
        HostName = builder.Configuration["RabbitMq:Host"] ?? "localhost",
        UserName = builder.Configuration["RabbitMq:Username"] ?? "guest",
        Password = builder.Configuration["RabbitMq:Password"] ?? "guest",
        Port = rabbitPort,
        AutomaticRecoveryEnabled = true
    }.CreateConnectionAsync();
    builder.Services.AddSingleton<IConnection>(rabbitConnection);

    builder.Services.AddHealthChecks()
        // Reaproveita o IMongoClient singleton do DI (resolvido em runtime) — evita abrir
        // um segundo client/pool de conexões só para o health check.
        .AddMongoDb(sp => sp.GetRequiredService<IMongoClient>(), name: "mongodb", tags: ["ready"])
        // AddRabbitMQ SEM factory: reusa a IConnection singleton do DI (sem leak); detecta o broker
        // fora (503) e reconecta sozinha quando ele volta (200).
        .AddRabbitMQ(name: "rabbitmq", tags: ["ready"]);

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
