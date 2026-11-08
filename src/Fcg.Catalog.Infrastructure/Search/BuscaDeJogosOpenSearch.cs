using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using OpenSearch.Client;

namespace Fcg.Catalog.Infrastructure.Search;

public sealed class BuscaDeJogosOpenSearch(
    IOpenSearchClient client,
    IIndiceDeJogos indiceDeJogos,
    IConfiguration configuration) : IBuscaDeJogos
{
    private readonly string _indice = configuration["OpenSearch:IndiceJogos"] ?? "jogos";

    public async Task<IReadOnlyList<JogoBuscaResponseDto>> BuscarAsync(string termo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(termo))
            return [];

        await indiceDeJogos.GarantirIndiceAsync(ct);

        var resposta = await client.SearchAsync<JogoIndiceDocumento>(pesquisa => pesquisa
            .Index(_indice)
            .Size(20)
            .Query(query => query.MultiMatch(multiMatch => multiMatch
                .Fields(fields => fields
                    .Field(jogo => jogo.Titulo, 3)
                    .Field("titulo.compacto", 2)
                    .Field(jogo => jogo.Descricao))
                .Query(termo)
                .Fuzziness(Fuzziness.Auto)))
            .Sort(sort => sort.Descending(SortSpecialField.Score)), ct);

        if (!resposta.IsValid)
            throw new InvalidOperationException($"Falha ao buscar jogos no OpenSearch. {resposta.DebugInformation}");

        return resposta.Hits.Select(hit =>
        {
            var jogo = hit.Source
                ?? throw new InvalidOperationException("O OpenSearch retornou um resultado de busca sem documento.");
            var score = hit.Score
                ?? throw new InvalidOperationException("O OpenSearch retornou um resultado de busca sem score.");

            return new JogoBuscaResponseDto(jogo.Id, jogo.Titulo, jogo.Descricao, jogo.Preco, score);
        }).ToArray();
    }

    private sealed record JogoIndiceDocumento(
        [property: PropertyName("id")] string Id,
        [property: PropertyName("titulo")] string Titulo,
        [property: PropertyName("descricao")] string Descricao,
        [property: PropertyName("genero")] string Genero,
        [property: PropertyName("preco")] decimal Preco,
        [property: PropertyName("ativo")] bool Ativo);
}
