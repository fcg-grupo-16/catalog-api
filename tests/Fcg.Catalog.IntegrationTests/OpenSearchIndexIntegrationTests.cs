using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Interfaces;
using Fcg.Catalog.Application.Services;
using Fcg.Catalog.Domain.Enums;
using Fcg.Catalog.Infrastructure.Search;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OpenSearch.Client;
using Testcontainers.OpenSearch;

namespace Fcg.Catalog.IntegrationTests;

[Collection(OpenSearchCollection.Name)]
public sealed class OpenSearchIndexIntegrationTests : IAsyncLifetime
{
    private readonly string _indexName = $"jogos-it-{Guid.NewGuid():N}";
    private readonly OpenSearchContainer _container = new OpenSearchBuilder("opensearchproject/opensearch:2.17.1")
        .WithSecurityEnabled(false)
        .Build();

    [Fact]
    public async Task CriarAtualizarERemover_MantemIndiceSincronizado()
    {
        var client = new OpenSearchClient(new ConnectionSettings(new Uri(_container.GetConnectionString()))
            .DefaultIndex(_indexName)
            .RequestTimeout(TimeSpan.FromSeconds(10)));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenSearch:IndiceJogos"] = _indexName
            })
            .Build();
        IIndiceDeJogos indice = new IndiceDeJogosOpenSearch(client, configuration);

        var jogoOriginal = new JogoResponseDto(
            "jogo-integration-1", "CodeQuest", "Aventura", GeneroJogo.RPG, 19.90m, "BRL", DateTime.UtcNow, true);
        var jogoAtualizado = jogoOriginal with { Titulo = "CodeQuest Remastered" };
        var inner = new Mock<IJogoService>();
        inner.Setup(service => service.CriarAsync(It.IsAny<CriarJogoRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jogoOriginal);
        inner.Setup(service => service.AtualizarAsync(
                jogoOriginal.Id, It.IsAny<AtualizarJogoRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jogoAtualizado);
        var service = new IndexedJogoService(inner.Object, indice, Mock.Of<ILogger<IndexedJogoService>>());

        await service.CriarAsync(new CriarJogoRequestDto(
            jogoOriginal.Titulo, jogoOriginal.Descricao, jogoOriginal.Genero, jogoOriginal.Preco, jogoOriginal.DataLancamento));
        (await PesquisarAsync(client, "CodeQuest")).Single().Titulo.Should().Be("CodeQuest");

        await service.AtualizarAsync(jogoOriginal.Id, new AtualizarJogoRequestDto(
            jogoAtualizado.Titulo, jogoAtualizado.Descricao, jogoAtualizado.Genero, jogoAtualizado.Preco));
        (await PesquisarAsync(client, "CodeQuest Remastered")).Single().Titulo.Should().Be("CodeQuest Remastered");

        await service.RemoverAsync(jogoOriginal.Id);
        (await PesquisarAsync(client, "CodeQuest Remastered")).Should().BeEmpty();
    }

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private async Task<IReadOnlyCollection<JogoIndiceTeste>> PesquisarAsync(OpenSearchClient client, string titulo)
    {
        var resposta = await client.SearchAsync<JogoIndiceTeste>(search => search
            .Index(_indexName)
            .Query(query => query.Match(match => match
                .Field(jogo => jogo.Titulo)
                .Query(titulo))));

        Assert.True(resposta.IsValid, resposta.DebugInformation);
        return resposta.Documents;
    }

    private sealed record JogoIndiceTeste(
        [property: PropertyName("id")] string Id,
        [property: PropertyName("titulo")] string Titulo,
        [property: PropertyName("descricao")] string Descricao,
        [property: PropertyName("genero")] string Genero,
        [property: PropertyName("preco")] decimal Preco,
        [property: PropertyName("ativo")] bool Ativo);
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OpenSearchCollection
{
    public const string Name = "opensearch-integration";
}
