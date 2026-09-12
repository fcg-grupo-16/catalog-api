using System.Net;
using Fcg.Catalog.IntegrationTests.Infrastructure;

namespace Fcg.Catalog.IntegrationTests;

public sealed class MetricsEndpointTests(FcgWebAppFactory factory) : IClassFixture<FcgWebAppFactory>
{
    [Fact(DisplayName = "GET /metrics expõe as métricas HTTP no formato Prometheus")]
    public async Task Metrics_ExpoeMetricasHttp()
    {
        var client = factory.CreateClient();

        await client.GetAsync("/health/live");

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("# TYPE", body);
        Assert.Contains("http_server_request_duration_seconds", body);
    }
}
