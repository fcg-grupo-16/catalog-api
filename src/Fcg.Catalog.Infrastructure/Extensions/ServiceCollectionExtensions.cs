using System.Globalization;
using System.Text;
using Fcg.Catalog.Application.Interfaces;
using Fcg.Catalog.Application.Services;
using Fcg.Catalog.Domain.Exceptions;
using Fcg.Catalog.Domain.Repositories;
using Fcg.Catalog.Infrastructure.Messaging;
using Fcg.Catalog.Infrastructure.Persistence;
using Fcg.Catalog.Infrastructure.Repositories;
using Fcg.Catalog.Infrastructure.Settings;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Microsoft.OpenApi;

namespace Fcg.Catalog.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMongoDb(this IServiceCollection services, IConfiguration configuration)
    {
        var mongoSettings = configuration.GetSection(MongoDbSettings.SectionName).Get<MongoDbSettings>()
            ?? throw new InvalidOperationException("MongoDbSettings não configurado.");

        services.Configure<MongoDbSettings>(configuration.GetSection(MongoDbSettings.SectionName));

        var mongoClient = new MongoClient(mongoSettings.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoSettings.DatabaseName);

        // IMongoClient/IMongoDatabase são exigidos pelo outbox transacional do MassTransit
        // (AddMongoDbOutbox), que compartilha o mesmo MongoClient do AppDbContext.
        services.AddSingleton<IMongoClient>(mongoClient);
        services.AddSingleton(mongoDatabase);

        services.AddDbContext<AppDbContext>(options =>
            options.UseMongoDB(mongoClient, mongoSettings.DatabaseName));

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("JwtSettings não configurado.");

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("ApenasAdmin", policy =>
                policy.RequireRole("Administrador"));
            options.AddPolicy("UsuarioAutenticado", policy =>
                policy.RequireAuthenticatedUser());
        });

        return services;
    }

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IJogoRepository, JogoRepository>();
        services.AddScoped<IBibliotecaRepository, BibliotecaRepository>();
        services.AddScoped<IPedidoRepository, PedidoRepository>();

        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        services.AddScoped<IJogoService, JogoService>();
        services.AddScoped<IBibliotecaService, BibliotecaService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<IPedidoService, PedidoService>();

        return services;
    }

    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            // Prefixo por serviço garante filas distintas entre microsserviços que
            // consomem o mesmo evento (pub/sub fanout, não competing consumers).
            x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("catalog", false));
            // Definition habilita o inbox transacional (dedup) no endpoint do consumer.
            x.AddConsumer<PaymentProcessedConsumer, PaymentProcessedConsumerDefinition>();

            // Outbox transacional (mesmo padrão da users-api): mensagens publicadas são
            // gravadas no Mongo na MESMA transação da entidade e entregues ao broker por um
            // serviço de entrega. Exige MongoDB replica set (transações multi-documento).
            x.AddMongoDbOutbox(o =>
            {
                o.QueryDelay = TimeSpan.FromSeconds(5);
                o.ClientFactory(provider => provider.GetRequiredService<IMongoClient>());
                o.DatabaseFactory(provider => provider.GetRequiredService<IMongoDatabase>());
                o.DuplicateDetectionWindow = TimeSpan.FromSeconds(30);
                o.UseBusOutbox();
            });

            x.UsingRabbitMq((ctx, cfg) =>
            {
                var host = configuration["RabbitMq:Host"] ?? "localhost";
                var user = configuration["RabbitMq:Username"] ?? "guest";
                var pass = configuration["RabbitMq:Password"] ?? "guest";

                // Porta opcional (RabbitMq:Port) — permite porta dinâmica em testes de integração;
                // sem ela, usa a porta padrão do AMQP (5672).
                if (ushort.TryParse(configuration["RabbitMq:Port"], out var port))
                {
                    cfg.Host(host, port, "/", h => { h.Username(user); h.Password(pass); });
                }
                else
                {
                    cfg.Host(host, "/", h => { h.Username(user); h.Password(pass); });
                }

                // Scheduler de mensagens atrasadas — usa o plugin rabbitmq_delayed_message_exchange
                // do broker (habilitado na imagem custom do repo orchestration). Necessário para o
                // delayed redelivery abaixo.
                cfg.UseDelayedMessageScheduler();

                // Redelivery atrasado (second-level retry): esgotado o retry imediato, a mensagem é
                // devolvida ao broker com intervalos CRESCENTES (default 1min → 5min → 15min) — absorve
                // falhas prolongadas de dependência (ex.: Mongo indisponível) sem estourar para a _error
                // cedo. Configurável via RabbitMq:DelayedRedeliverySeconds (usado curto nos testes).
                // Exceções de domínio são determinísticas: Ignore para não redeliver.
                var delayedIntervals = ParseDelayedIntervals(configuration["RabbitMq:DelayedRedeliverySeconds"]);
                cfg.UseDelayedRedelivery(r =>
                {
                    r.Intervals(delayedIntervals);
                    r.Ignore<DomainException>();
                });

                // Retry imediato (first-level), EXPONENCIAL com limite. Erros de negócio/validação
                // (DomainException e subtipos) são determinísticos — Ignore os manda direto para a
                // _error sem retentar; faltas transitórias de infra (não-DomainException) são retentadas.
                var immediateRetries = int.TryParse(configuration["RabbitMq:ImmediateRetryCount"], out var ir) ? ir : 3;
                cfg.UseMessageRetry(r =>
                {
                    r.Exponential(immediateRetries, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(3));
                    r.Ignore<DomainException>();
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }

    // Parse tolerante de RabbitMq:DelayedRedeliverySeconds. Ignora entradas inválidas/não-positivas
    // e faz fallback para os defaults (60/300/900s) se a config estiver ausente/vazia/toda inválida —
    // um valor ruim na config não pode derrubar o serviço no startup.
    private static TimeSpan[] ParseDelayedIntervals(string? raw)
    {
        var defaults = new[] { TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(900) };
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaults;
        }

        var parsed = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v > 0
                ? TimeSpan.FromSeconds(v)
                : (TimeSpan?)null)
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .ToArray();

        return parsed.Length > 0 ? parsed : defaults;
    }

    public static void AddSwaggerExtension(this IServiceCollection service)
    {
        service.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "FIAP Cloud Games - Catalog API",
                Version = "v1",
                Description = "API REST para o catálogo de jogos e biblioteca do usuário da plataforma FIAP Cloud Games."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Informe apenas o token JWT (sem o prefixo 'Bearer').",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });

            var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly);
            foreach (var xmlFile in xmlFiles)
            {
                options.IncludeXmlComments(xmlFile);
            }
        });
    }
}
