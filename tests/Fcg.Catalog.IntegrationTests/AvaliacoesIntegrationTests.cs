using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fcg.Catalog.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Fcg.Catalog.IntegrationTests;

/// <summary>Testes de integração das avaliações e da operação atrás do gateway.</summary>
/// <remarks>
/// <b>IClassFixture, e não IAsyncLifetime na classe de teste.</b> O xUnit cria uma INSTÂNCIA DA
/// CLASSE por método de teste; com a factory num campo de instância, cada teste subia o seu próprio
/// par Mongo+RabbitMQ. Medido: estes 12 testes levavam 2 min 21 s assim, contra 23 s para os 10
/// testes de <c>PedidoFlowIntegrationTests</c>, que compartilham a factory. Além do tempo, o
/// teardown não acontecia (ver o remarks de <see cref="FcgWebAppFactory.DisposeContainersAsync"/>)
/// e os containers vazavam. O fixture é criado UMA vez por classe e descartado pelo xUnit.
/// </remarks>
[Collection(PlataformaCollection.Nome)]
public class AvaliacoesIntegrationTests(FcgWebAppFactory factory)
{

    private HttpClient UserClient(string userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.Gerar(userId));
        return client;
    }

    private HttpClient AdminClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.Gerar("admin-1", "Administrador"));
        return client;
    }

    /// <summary>
    /// Descreve a resposta inteira — status, <c>Location</c> e corpo — para entrar na mensagem de
    /// falha da asserção.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Existe por causa da <see href="https://github.com/fcg-grupo-16/catalog-api/issues/24">#24</see>.
    /// A falha intermitente deste arquivo segue sem causa raiz por um motivo específico: a única
    /// evidência que sobrou foi <c>Expected ... to be Created</c>, e isso não distingue um 500 do
    /// domínio de um 400 de validação — causas completamente diferentes.
    /// </para>
    /// <para>
    /// A taxa é baixa demais para reproduzir sob demanda — a contagem de execuções limpas está na
    /// issue, que é datada, e não aqui, onde envelheceria em silêncio. A próxima ocorrência pode
    /// demorar e precisa se explicar sozinha. Ler o corpo aqui é seguro: o <c>HttpClient</c> usa
    /// <c>ResponseContentRead</c> por padrão e bufferiza o conteúdo, então um
    /// <c>ReadFromJsonAsync</c> depois não pega o stream vazio.
    /// </para>
    /// </remarks>
    private static async Task<string> DescreverAsync(HttpResponseMessage resposta)
    {
        var corpo = await resposta.Content.ReadAsStringAsync();

        return $"status={(int)resposta.StatusCode} {resposta.ReasonPhrase}"
            + $"; Location={resposta.Headers.Location?.ToString() ?? "(ausente)"}"
            + $"; corpo={(string.IsNullOrWhiteSpace(corpo) ? "(vazio)" : corpo)}";
    }

    private static async Task<string> CriarJogoAsyncWithFactory(FcgWebAppFactory factory)
    {
        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.Gerar("admin-factory", "Administrador"));

        var resp = await admin.PostAsJsonAsync("/api/v1/jogos", new
        {
            titulo = $"Jogo Avaliacao {Guid.NewGuid():N}",
            descricao = "Jogo para avaliação de integração.",
            genero = 2,
            preco = 49.90m,
            dataLancamento = "2024-01-01T00:00:00Z"
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created, "a criação do jogo respondeu {0}", await DescreverAsync(resp));
        var jogo = await resp.Content.ReadFromJsonAsync<JogoDto>();
        return jogo!.Id;
    }

    private async Task<string> CriarJogoAsync()
    {
        using var admin = AdminClient();
        var resp = await admin.PostAsJsonAsync("/api/v1/jogos", new
        {
            titulo = $"Jogo Avaliacao {Guid.NewGuid():N}",
            descricao = "Jogo para avaliação de integração.",
            genero = 2,
            preco = 49.90m,
            dataLancamento = "2024-01-01T00:00:00Z"
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created, "a criação do jogo respondeu {0}", await DescreverAsync(resp));
        var jogo = await resp.Content.ReadFromJsonAsync<JogoDto>();
        return jogo!.Id;
    }

    private async Task<string> CriarAvaliacaoAsync(string userId, string jogoId, int nota = 5, string? comentario = null, string? titulo = null, IReadOnlyList<string>? tags = null, IReadOnlyDictionary<string, string>? contexto = null)
    {
        using var user = UserClient(userId);
        var payload = new
        {
            jogoId,
            nota,
            comentario = comentario ?? "Comentário de teste.",
            titulo,
            tags,
            contexto
        };

        var resp = await user.PostAsJsonAsync("/api/v1/avaliacoes", payload);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"requisição com usuário {userId} para jogo {jogoId}");

        var avaliacao = await resp.Content.ReadFromJsonAsync<AvaliacaoDto>();
        avaliacao.Should().NotBeNull();
        return avaliacao!.Id;
    }

    [Fact]
    public async Task Criar_AtrasDoGateway_LocationUsaSchemeEHostEncaminhados()
    {
        // Factory PRÓPRIA: este teste precisa da app com outra configuração de ForwardedHeaders,
        // que é lida no startup. Não dá para reusar o fixture da classe.
        // `await using` seria um bug aqui: ligaria no DisposeAsync herdado de WebApplicationFactory
        // e deixaria Mongo e RabbitMQ rodando. Daí o try/finally com DisposeContainersAsync.
        var gatewayFactory = FcgWebAppFactory.ComForwardedHeaders(true, "127.0.0.1/32");
        try
        {
            await gatewayFactory.InitializeAsync();

            var jogoId = await CriarJogoAsyncWithFactory(gatewayFactory);
            using var client = gatewayFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", JwtTokenHelper.Gerar("user-forwarded-location"));

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/avaliacoes")
            {
                Content = JsonContent.Create(new
                {
                    jogoId,
                    nota = 5,
                    comentario = "Avaliacao via gateway",
                    titulo = "Gateway",
                    tags = new[] { "forwarded" }
                })
            };
            request.Headers.Add("X-Forwarded-Proto", "https");
            request.Headers.Add("X-Forwarded-Host", "api.fcg.local");
            request.Headers.Add("X-Forwarded-For", "203.0.113.10");

            using var response = await client.SendAsync(request);

            var descricao = await DescreverAsync(response);

            response.StatusCode.Should().Be(HttpStatusCode.Created, "a resposta foi {0}", descricao);
            response.Headers.Location.Should().NotBeNull("a resposta foi {0}", descricao);
            // Sem ForwardedHeaders o Location sairia com o host INTERNO do cluster, inalcançável
            // pelo cliente. É o bug concreto que justifica a issue neste serviço.
            response.Headers.Location!.ToString().Should()
                .StartWith("https://api.fcg.local/", "a resposta foi {0}", descricao);
        }
        finally
        {
            await gatewayFactory.DisposeContainersAsync();
        }
    }

    [Fact]
    public async Task Criar_SemForwardedHeaders_IgnoraHeadersDoCliente()
    {
        // Factory PRÓPRIA: este teste precisa da app com outra configuração de ForwardedHeaders,
        // que é lida no startup. Não dá para reusar o fixture da classe.
        // `await using` seria um bug aqui: ligaria no DisposeAsync herdado de WebApplicationFactory
        // e deixaria Mongo e RabbitMQ rodando. Daí o try/finally com DisposeContainersAsync.
        var gatewayFactory = FcgWebAppFactory.ComForwardedHeaders(false, "127.0.0.1/32");
        try
        {
            await gatewayFactory.InitializeAsync();

            var jogoId = await CriarJogoAsyncWithFactory(gatewayFactory);
            using var client = gatewayFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", JwtTokenHelper.Gerar("user-forwarded-disabled"));

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/avaliacoes")
            {
                Content = JsonContent.Create(new
                {
                    jogoId,
                    nota = 5,
                    comentario = "Sem forwarded headers",
                    titulo = "Sem gateway",
                    tags = new[] { "disabled" }
                })
            };
            request.Headers.Add("X-Forwarded-Proto", "https");
            request.Headers.Add("X-Forwarded-Host", "evil.example.com");
            request.Headers.Add("X-Forwarded-For", "203.0.113.10");

            using var response = await client.SendAsync(request);

            var descricao = await DescreverAsync(response);

            response.StatusCode.Should().Be(HttpStatusCode.Created, "a resposta foi {0}", descricao);
            response.Headers.Location.Should().NotBeNull("a resposta foi {0}", descricao);
            // Garantia de SEGURANÇA: desligado, um cliente não consegue manipular as URLs que a
            // API gera mandando X-Forwarded-Host.
            response.Headers.Location!.ToString().Should()
                .NotContain("evil.example.com", "a resposta foi {0}", descricao);
        }
        finally
        {
            await gatewayFactory.DisposeContainersAsync();
        }
    }

    [Fact]
    public async Task Criar_RetornaCreated()
    {
        var jogoId = await CriarJogoAsync();
        using var client = UserClient("user-avaliacao-create");

        var resp = await client.PostAsJsonAsync("/api/v1/avaliacoes", new
        {
            jogoId,
            nota = 5,
            comentario = "Excelente!",
            titulo = "Melhor do ano",
            tags = new[] { "ação", "rpg" },
            contexto = new Dictionary<string, string> { ["plataforma"] = "PC" }
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        resp.Headers.Location.Should().NotBeNull();

        var body = await resp.Content.ReadFromJsonAsync<AvaliacaoDto>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBeNullOrWhiteSpace();
        body.JogoId.Should().Be(jogoId);
        body.UsuarioId.Should().Be("user-avaliacao-create");
    }

    [Fact(DisplayName = "Avaliação duplicada é logada como 409, e não como 500/Error")]
    public async Task Criar_Duplicada_LogaStatusRespondido()
    {
        // FACTORY PRÓPRIA, e o motivo é sutil: o `UseSerilogRequestLogging` escreve no `Log.Logger`
        // ESTÁTICO do Serilog, enquanto o middleware de exceção escreve pelo ILogger<T> da DI. Esta
        // suíte constrói três hosts no mesmo processo (o da collection e os dois de
        // `ComForwardedHeaders`), e o último a subir sobrescreve o logger estático — então um sink
        // registrado na factory compartilhada recebe o Warning do middleware mas PERDE a linha de
        // request. Medido: o teste passava isolado e falhava na suíte, capturando 1 evento
        // (`Warning:/api/v1/avaliacoes`) e nenhum com StatusCode.
        //
        // ⚠️ O custo é um par de containers a mais, que é justamente o que a #24 investiga. Se aquela
        // issue concluir pela redução de factories, este teste entra na conta.
        var proprio = new FcgWebAppFactory();

        try
        {
            await proprio.InitializeAsync();

            var jogoId = await CriarJogoAsyncWithFactory(proprio);
            using var client = proprio.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", JwtTokenHelper.Gerar("user-log-conflito"));

            var corpo = new { jogoId, nota = 5, comentario = "duplicada", titulo = "Dup", tags = new[] { "dup" } };

            (await client.PostAsJsonAsync("/api/v1/avaliacoes", corpo)).StatusCode
                .Should().Be(HttpStatusCode.Created);

            proprio.Log.Limpar();

            var conflito = await client.PostAsJsonAsync("/api/v1/avaliacoes", corpo);
            conflito.StatusCode.Should().Be(HttpStatusCode.Conflict);

            // O que o CLIENTE recebe sempre esteve certo. O que estava errado era o log: o
            // request-logging era interno ao handler de exceção, via a exceção escapar e registrava
            // Error/500 para uma requisição que respondeu 409 (issue #26).
            var requisicoes = proprio.Log.DeRequisicao("/api/v1/avaliacoes")
                .Where(e => CapturaDeLog.StatusDe(e) is not null)
                .ToList();

            requisicoes.Should().NotBeEmpty("o request-logging precisa registrar a requisição");
            requisicoes.Should().OnlyContain(e => CapturaDeLog.StatusDe(e) == 409);
            requisicoes.Should().NotContain(e => e.Level >= Serilog.Events.LogEventLevel.Error);

            // Guarda contra regressão de ordem: pondo o request-logging ANTES do push de TraceId, a
            // linha de request perde a correlação com o trace sem nenhum teste reclamar.
            requisicoes.Should().OnlyContain(
                e => e.Properties.ContainsKey("TraceId"),
                "a linha de request precisa continuar correlacionada com o trace");
        }
        finally
        {
            await proprio.DisposeContainersAsync();
        }
    }

    [Fact]
    public async Task Criar_DuasVezesMesmoUsuarioEJogo_RetornaConflict()
    {
        var jogoId = await CriarJogoAsync();
        using var client = UserClient("user-avaliacao-duplicado");

        var primeiro = await client.PostAsJsonAsync("/api/v1/avaliacoes", new
        {
            jogoId,
            nota = 4,
            comentario = "Primeira avaliação."
        });
        primeiro.StatusCode.Should().Be(HttpStatusCode.Created);

        var segundo = await client.PostAsJsonAsync("/api/v1/avaliacoes", new
        {
            jogoId,
            nota = 5,
            comentario = "Segunda avaliação duplicada."
        });

        segundo.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Criar_JogoInexistente_RetornaNotFound()
    {
        using var client = UserClient("user-jogo-inexistente");

        var resp = await client.PostAsJsonAsync("/api/v1/avaliacoes", new
        {
            jogoId = "507f1f77bcf86cd799439011",
            nota = 4,
            comentario = "Jogo inexistente"
        });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Criar_NotaInvalida_RetornaUnprocessableEntity()
    {
        var jogoId = await CriarJogoAsync();
        using var client = UserClient("user-nota-invalida");

        var resp = await client.PostAsJsonAsync("/api/v1/avaliacoes", new
        {
            jogoId,
            nota = 0,
            comentario = "Nota inválida"
        });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Listar_RetornaMaisRecentesPrimeiro()
    {
        var jogoId = await CriarJogoAsync();

        await CriarAvaliacaoAsync("user-list-1", jogoId, 3, "Primeira");
        await Task.Delay(100);
        await CriarAvaliacaoAsync("user-list-2", jogoId, 5, "Segunda");
        await Task.Delay(100);
        await CriarAvaliacaoAsync("user-list-3", jogoId, 4, "Terceira");

        using var client = UserClient("user-list-reader");
        var resp = await client.GetAsync($"/api/v1/jogos/{jogoId}/avaliacoes?pagina=1&tamanhoPagina=10");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagina = await resp.Content.ReadFromJsonAsync<PaginaDto<AvaliacaoDto>>();
        pagina.Should().NotBeNull();
        pagina!.Total.Should().Be(3, "o total vem do ContarPorJogoAsync, não do tamanho da página");

        var itens = pagina.Itens;
        itens.Should().HaveCount(3);
        itens[0].DataCriacao.Should().BeOnOrAfter(itens[1].DataCriacao);
        itens[1].DataCriacao.Should().BeOnOrAfter(itens[2].DataCriacao);
    }

    [Fact]
    public async Task Resumo_CalculaMediaEDistribuicao()
    {
        var jogoId = await CriarJogoAsync();

        await CriarAvaliacaoAsync("user-resumo-1", jogoId, 5, "Ótima");
        await CriarAvaliacaoAsync("user-resumo-2", jogoId, 4, "Boa");
        await CriarAvaliacaoAsync("user-resumo-3", jogoId, 4, "Outra boa");

        using var client = UserClient("user-resumo-reader");
        var resp = await client.GetAsync($"/api/v1/jogos/{jogoId}/avaliacoes/resumo");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var resumo = await resp.Content.ReadFromJsonAsync<ResumoDto>();

        resumo.Should().NotBeNull();
        resumo!.Total.Should().Be(3);
        resumo.MediaNota.Should().BeApproximately(4.33m, 0.01m);
        resumo.Distribuicao[1].Should().Be(0);
        resumo.Distribuicao[2].Should().Be(0);
        resumo.Distribuicao[3].Should().Be(0);
        resumo.Distribuicao[4].Should().Be(2);
        resumo.Distribuicao[5].Should().Be(1);
    }

    [Fact]
    public async Task Resumo_JogoSemAvaliacoes_RetornaZerado()
    {
        var jogoId = await CriarJogoAsync();
        using var client = UserClient("user-resumo-vazio");

        var resp = await client.GetAsync($"/api/v1/jogos/{jogoId}/avaliacoes/resumo");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var resumo = await resp.Content.ReadFromJsonAsync<ResumoDto>();

        resumo.Should().NotBeNull();
        resumo!.Total.Should().Be(0);
        resumo.MediaNota.Should().Be(0m);
        resumo.Distribuicao.Keys.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
        foreach (var nota in new[] { 1, 2, 3, 4, 5 })
        {
            resumo.Distribuicao[nota].Should().Be(0);
        }
    }

    [Fact]
    public async Task VotoUtil_IncrementaAtomicamente()
    {
        var jogoId = await CriarJogoAsync();
        var avaliacaoId = await CriarAvaliacaoAsync("user-votos-1", jogoId, 5, "Votos úteis");

        using var client = UserClient("user-votos-reader");
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => client.PostAsync($"/api/v1/avaliacoes/{avaliacaoId}/util", null));

        var responses = await Task.WhenAll(tasks);
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        var avaliacao = await GetAvaliacaoAsync(avaliacaoId, client);
        avaliacao.VotosUteis.Should().Be(20);
    }

    [Fact]
    public async Task Remover_DeOutroUsuario_RetornaForbidden()
    {
        var jogoId = await CriarJogoAsync();
        var avaliacaoId = await CriarAvaliacaoAsync("user-remocao-autor", jogoId, 4, "Para remover");

        using var client = UserClient("user-remocao-outra");
        var resp = await client.DeleteAsync($"/api/v1/avaliacoes/{avaliacaoId}");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ContextoFlexivel_PersisteChavesArbitrarias()
    {
        var jogoId = await CriarJogoAsync();
        using var client = UserClient("user-contexto");

        var payload = new
        {
            jogoId,
            nota = 5,
            comentario = "Contexto flexível",
            contexto = new Dictionary<string, string>
            {
                ["plataforma"] = "PC",
                ["modQualquer"] = "x"
            }
        };

        var resp = await client.PostAsJsonAsync("/api/v1/avaliacoes", payload);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var avaliacao = await resp.Content.ReadFromJsonAsync<AvaliacaoDto>();
        avaliacao.Should().NotBeNull();
        avaliacao!.Contexto.Should().ContainKey("plataforma").WhoseValue.Should().Be("PC");
        avaliacao.Contexto.Should().ContainKey("modQualquer").WhoseValue.Should().Be("x");
    }

    private static async Task<AvaliacaoDto> GetAvaliacaoAsync(string id, HttpClient client)
    {
        var resp = await client.GetAsync($"/api/v1/avaliacoes/{id}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var avaliacao = await resp.Content.ReadFromJsonAsync<AvaliacaoDto>();
        avaliacao.Should().NotBeNull();
        return avaliacao!;
    }

    private sealed record JogoDto(string Id);

    private sealed record PaginaDto<T>(IReadOnlyList<T> Itens, int Pagina, int TamanhoPagina, long Total);
    private sealed record AvaliacaoDto(
        string Id,
        string JogoId,
        string UsuarioId,
        int Nota,
        string? Titulo,
        string Comentario,
        IReadOnlyList<string> Tags,
        IReadOnlyDictionary<string, string> Contexto,
        int VotosUteis,
        DateTime DataCriacao);

    private sealed record ResumoDto(
        string JogoId,
        long Total,
        decimal MediaNota,
        IReadOnlyDictionary<int, long> Distribuicao);
}
