using Fcg.Catalog.Api.Extensions;
using Fcg.Catalog.Api.Middlewares;
using Fcg.Catalog.Application.Validators;
using Fcg.Catalog.Domain.Repositories;
using Fcg.Catalog.Infrastructure.Extensions;
using Fcg.Catalog.Infrastructure.Seed;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MongoDB.Driver;
using RabbitMQ.Client;
using Serilog;
using StackExchange.Redis;

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
    // nova a cada readiness sem fechá-la (leak que saturava o broker). A factory cria a conexão UMA
    // vez e a reusa em todas as checagens — com auto-recovery para reconectar quando o broker volta.
    // O lock (double-checked) evita a criação concorrente se dois probes chegarem simultaneamente; se
    // a conexão estiver fechada (recovery esgotado) ela é descartada e RECRIADA na próxima check —
    // por isso não usamos Lazy<Task<IConnection>>, que cachearia uma Task falhada (broker fora no 1º
    // check) e deixaria o readiness preso em 503 mesmo após o broker voltar. Lazy e assíncrona (sem
    // sync-over-async, sem bloquear o startup): o processo sobe mesmo com o broker fora/lento, o check
    // reporta 503, e uma tentativa futura reconecta (200). Respeita RabbitMq:Port (porta dinâmica nos
    // testes; 5672 em compose/k8s).
    var healthRabbitLock = new SemaphoreSlim(1, 1);
    IConnection? healthRabbitConnection = null;

    builder.Services.AddHealthChecks()
        // Reaproveita o IMongoClient singleton do DI (resolvido em runtime) — evita abrir
        // um segundo client/pool de conexões só para o health check.
        .AddMongoDb(sp => sp.GetRequiredService<IMongoClient>(), name: "mongodb", tags: ["ready"])
        .AddRabbitMQ(
            factory: async sp =>
            {
                var current = Volatile.Read(ref healthRabbitConnection);
                if (current?.IsOpen == true)
                    return current;

                var configuration = sp.GetRequiredService<IConfiguration>();
                var rabbitPort = ushort.TryParse(configuration["RabbitMq:Port"], out var parsedPort) ? parsedPort : (ushort)5672;
                await healthRabbitLock.WaitAsync();
                try
                {
                    current = Volatile.Read(ref healthRabbitConnection);
                    if (current?.IsOpen == true)
                        return current;

                    // A conexão anterior está fechada (recovery esgotado) — descarta antes de recriar.
                    if (current is not null)
                    {
                        await current.DisposeAsync();
                        Volatile.Write(ref healthRabbitConnection, null);
                    }

                    var created = await new ConnectionFactory
                    {
                        HostName = configuration["RabbitMq:Host"] ?? "localhost",
                        UserName = configuration["RabbitMq:Username"] ?? "guest",
                        Password = configuration["RabbitMq:Password"] ?? "guest",
                        Port = rabbitPort,
                        AutomaticRecoveryEnabled = true
                    }.CreateConnectionAsync();
                    Volatile.Write(ref healthRabbitConnection, created);
                    return created;
                }
                finally
                {
                    healthRabbitLock.Release();
                }
            },
            name: "rabbitmq",
            tags: ["ready"]);

    // Registrado só quando o cache está REALMENTE ativo. A connection string sozinha não basta:
    // o SealedSecret injeta Redis__ConnectionString mesmo com Redis__Enabled ausente/false, e aí
    // o serviço passaria a depender de um Redis que nem usa.
    var redisAtivo = builder.Configuration.GetValue("Redis:Enabled", true)
        && !string.IsNullOrWhiteSpace(builder.Configuration["Redis:ConnectionString"]);

    if (redisAtivo)
    {
        builder.Services.AddHealthChecks()
            .AddRedis(
                // Reusa o multiplexer do DI em vez de abrir uma segunda conexão só para o check.
                connectionMultiplexerFactory: static sp => sp.GetRequiredService<IConnectionMultiplexer>(),
                name: "redis",
                // ⚠️ tag "cache", NÃO "ready" — igual ao users-api. O Redis aqui é degradação, não
                // indisponibilidade: o RedisCacheService é fail-open em todos os caminhos. Se este
                // check entrasse em "ready", um Redis fora tiraria o pod do balanceador e derrubaria
                // o catálogo inteiro por causa de um cache. É o oposto do que o cache existe para ser.
                tags: ["cache"]);
    }

    builder.Services.AddSwaggerExtension();
    builder.Services.AddValidatorsFromAssemblyContaining<CriarJogoValidator>();
    builder.Services.AddGatewayForwardedHeaders(builder.Configuration);

    builder.Services.AddMongoDb(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddMessaging(builder.Configuration);
    builder.Services.AddObservability(builder.Configuration, builder.Environment);

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.Use(async (context, next) =>
    {
        var activity = System.Diagnostics.Activity.Current;
        if (activity is not null)
        {
            using (Serilog.Context.LogContext.PushProperty("TraceId", activity.TraceId.ToString()))
            using (Serilog.Context.LogContext.PushProperty("SpanId", activity.SpanId.ToString()))
            {
                await next(context);
                return;
            }
        }

        await next(context);
    });

    // O REQUEST-LOGGING FICA FORA DO HANDLER DE EXCEÇÃO. Registrado DEPOIS, o Serilog virava o
    // middleware INTERNO: a exceção escapava por ele antes de chegar ao handler, ele a via como não
    // tratada e registrava nível `Error` com `StatusCode` 500 — enquanto o cliente recebia 409.
    // Medido: avaliação duplicada respondia 409 e o log gravava Error/500 (issue #26 — mesmo defeito
    // da users-api#29, corrigido lá do mesmo jeito).
    //
    // ⚠️ Precisa continuar DEPOIS do push de TraceId/SpanId: invertê-los faria a linha de request
    // perder a correlação com o trace. Foi o erro que quase passou na correção do users-api.
    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = static (httpContext, elapsed, ex) =>
        {
            if (ex is not null || httpContext.Response.StatusCode >= 500)
                return Serilog.Events.LogEventLevel.Error;

            var path = httpContext.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase))
                return Serilog.Events.LogEventLevel.Verbose;

            return Serilog.Events.LogEventLevel.Information;
        };
    });

    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

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
    app.MapPrometheusScrapingEndpoint();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false               // nenhum check de dependencia: so processo vivo
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")  // Mongo + RabbitMQ (Redis fica de fora: tag "cache")
    });

    try
    {
        await DatabaseSeed.SeedAsync(app.Services);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Seed de dados falhou. A aplicação continuará sem dados iniciais.");
    }

    try
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAvaliacaoRepository>().GarantirIndicesAsync();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Falha ao garantir os índices de avaliações.");
    }

    await app.RunAsync();

    // Libera recursos da conexão de health check após o host encerrar (sem concorrência possível).
    if (healthRabbitConnection is not null)
        await healthRabbitConnection.DisposeAsync();
    healthRabbitLock.Dispose();
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
