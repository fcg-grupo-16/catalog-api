using System.Net;
using System.Net.Http.Headers;
using Fcg.Catalog.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Fcg.Catalog.IntegrationTests;

/// <summary>
/// Contrato HTTP de <c>/search</c> e <c>/reindex</c>: autenticação, autorização e o que acontece
/// quando o OpenSearch não está configurado.
/// </summary>
/// <remarks>
/// A plataforma de teste NÃO configura <c>OpenSearch:Endpoint</c>, e isso é proposital: é o caminho
/// "ausente = desligado", em que o serviço sobe e as rotas de busca respondem 503 em vez de 500. A
/// busca de verdade (fuzzy e ordem por score) é coberta contra um OpenSearch real em
/// <see cref="OpenSearchIndexIntegrationTests"/>.
/// </remarks>
[Collection(PlataformaCollection.Nome)]
public sealed class BuscaEndpointTests(FcgWebAppFactory factory)
{
    private HttpClient Cliente(string? papel = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            papel is null ? JwtTokenHelper.Gerar("usuario-busca") : JwtTokenHelper.Gerar("admin-busca", papel));
        return client;
    }

    [Fact]
    public async Task Search_SemToken_Devolve401()
    {
        using var anonimo = factory.CreateClient();

        var resposta = await anonimo.GetAsync("/api/v1/jogos/search?q=code");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_SemOpenSearchConfigurado_Devolve503()
    {
        using var usuario = Cliente();

        var resposta = await usuario.GetAsync("/api/v1/jogos/search?q=code");

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Reindex_UsuarioComum_Devolve403()
    {
        using var usuario = Cliente();

        var resposta = await usuario.PostAsync("/api/v1/jogos/reindex", content: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reindex_SemOpenSearchConfigurado_Devolve503()
    {
        using var admin = Cliente("Administrador");

        var resposta = await admin.PostAsync("/api/v1/jogos/reindex", content: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
