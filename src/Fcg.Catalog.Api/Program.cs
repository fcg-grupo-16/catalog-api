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



    var rabbitUri = $"amqp://{builder.Configuration["RabbitMq:Username"]}:{builder.Configuration["RabbitMq:Password"]}@{builder.Configuration["RabbitMq:Host"]}:5672/";

    builder.Services.AddHealthChecks()
        // Reaproveita o IMongoClient singleton do DI (resolvido em runtime) — evita abrir
        // um segundo client/pool de conexões só para o health check.
        .AddMongoDb(sp => sp.GetRequiredService<IMongoClient>(), name: "mongodb", tags: ["ready"])
        .AddRabbitMQ(sp => new ConnectionFactory { Uri = new Uri(rabbitUri) }.CreateConnectionAsync(),
                name: "rabbitmq", tags: ["ready"]);

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
