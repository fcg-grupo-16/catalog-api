using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Fcg.Catalog.Api.Extensions;

/// <summary>
/// Instrumentação de observabilidade do serviço (Fase 3): métricas no formato Prometheus
/// (expostas em <c>/metrics</c>) e traces distribuídos exportados por OTLP.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Registra OpenTelemetry para métricas e traces.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Métricas.</b> Não criamos métrica customizada: o próprio ASP.NET Core publica o
    /// histograma <c>http.server.request.duration</c> com os atributos
    /// <c>http.response.status_code</c>, <c>http.request.method</c> e <c>http.route</c>. Um
    /// histograma entrega de uma vez as TRÊS métricas que a Fase 3 exige — latência (via
    /// <c>histogram_quantile</c>), contagem total (série <c>_count</c>) e contagem por status
    /// code (label). Métrica de negócio custom só se aparecer necessidade real.
    /// </para>
    /// <para>
    /// <b>Traces.</b> O MassTransit 8 propaga contexto de trace W3C entre publisher e consumer
    /// nativamente, então ligar o exportador OTLP aqui basta para o trace atravessar
    /// catalog-api → RabbitMQ → payments-api. Não é preciso instrumentar a mensageria.
    /// </para>
    /// <para>
    /// <b>Configuração.</b> Tudo por variável de ambiente padrão do OTel
    /// (<c>OTEL_EXPORTER_OTLP_ENDPOINT</c>, <c>OTEL_SERVICE_NAME</c>), provisionadas pelo
    /// ConfigMap do repo <c>orchestration</c> — 12-factor, nada hardcoded.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var serviceName = configuration["OTEL_SERVICE_NAME"] ?? "catalog-api";
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        var otel = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString() ?? "0.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = environment.EnvironmentName
                }));

        otel.WithMetrics(metrics => metrics
            .AddMeter("Fcg.Catalog.Cache")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter());

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            otel.WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context =>
                    {
                        var path = context.Request.Path.Value ?? string.Empty;
                        return !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
                            && !path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase);
                    };
                    options.RecordException = true;
                })
                .AddHttpClientInstrumentation()
                // NÃO há AddSource do MongoDB aqui de propósito. O driver 3.x só emite esses
                // spans com o pacote MongoDB.Driver.Core.Extensions.DiagnosticSources instalado;
                // sem ele, um AddSource com o nome do source é silenciosamente ignorado — o que
                // dá a falsa impressão de que o banco está instrumentado. Fica como melhoria
                // futura, explícita, em vez de uma linha que não faz nada.
                .AddSource("MassTransit")
                .AddOtlpExporter());
        }

        return services;
    }
}
