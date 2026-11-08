using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using OpenSearch.Client;
using OpenSearch.Net;

namespace Fcg.Catalog.Infrastructure.Search;

public sealed class IndiceDeJogosOpenSearch(
    IOpenSearchClient client,
    IConfiguration configuration) : IIndiceDeJogos
{
    private const string DefinicaoIndice = """
        {
          "settings": {
            "index": { "max_ngram_diff": 20 },
            "analysis": {
              "char_filter": {
                "remove_separadores": {
                  "type": "pattern_replace",
                  "pattern": "[^\\p{L}\\p{N}]",
                  "replacement": ""
                }
              },
              "filter": {
                "prefixos": { "type": "edge_ngram", "min_gram": 3, "max_gram": 20 }
              },
              "analyzer": {
                "pt_br": {
                  "tokenizer": "standard",
                  "filter": ["lowercase", "asciifolding", "brazilian_stem"]
                },
                "compacto_indice": {
                  "char_filter": ["remove_separadores"],
                  "tokenizer": "keyword",
                  "filter": ["lowercase", "asciifolding", "prefixos"]
                },
                "compacto_busca": {
                  "char_filter": ["remove_separadores"],
                  "tokenizer": "keyword",
                  "filter": ["lowercase", "asciifolding"]
                }
              }
            }
          },
          "mappings": {
            "properties": {
              "titulo": {
                "type": "text",
                "analyzer": "pt_br",
                "fields": {
                  "compacto": {
                    "type": "text",
                    "analyzer": "compacto_indice",
                    "search_analyzer": "compacto_busca"
                  }
                }
              },
              "descricao": { "type": "text", "analyzer": "pt_br" },
              "genero": { "type": "keyword" },
              "preco": { "type": "scaled_float", "scaling_factor": 100 },
              "ativo": { "type": "boolean" }
            }
          }
        }
        """;

    private readonly string _indice = configuration["OpenSearch:IndiceJogos"] ?? "jogos";
    private readonly SemaphoreSlim _garantirIndiceLock = new(1, 1);

    public async Task GarantirIndiceAsync(CancellationToken ct = default)
    {
        var existe = await client.Indices.ExistsAsync(new IndexExistsRequest(_indice), ct);
        if (existe.Exists)
            return;

        await _garantirIndiceLock.WaitAsync(ct);
        try
        {
            existe = await client.Indices.ExistsAsync(new IndexExistsRequest(_indice), ct);
            if (existe.Exists)
                return;

            var resposta = await client.LowLevel.DoRequestAsync<StringResponse>(
                OpenSearch.Net.HttpMethod.PUT,
                $"/{Uri.EscapeDataString(_indice)}",
                ct,
                PostData.String(DefinicaoIndice));

            if (resposta.HttpStatusCode is >= 200 and < 300)
                return;

            // Outra réplica pode criar o índice entre o Exists e o PUT.
            existe = await client.Indices.ExistsAsync(new IndexExistsRequest(_indice), ct);
            if (!existe.Exists)
                throw new InvalidOperationException(
                    $"Falha ao criar o índice OpenSearch '{_indice}': HTTP {resposta.HttpStatusCode}.");
        }
        finally
        {
            _garantirIndiceLock.Release();
        }
    }

    public async Task IndexarAsync(JogoResponseDto jogo, CancellationToken ct = default)
    {
        await GarantirIndiceAsync(ct);
        var resposta = await client.IndexAsync(ParaDocumento(jogo), request => request
            .Index(_indice)
            .Id(jogo.Id)
            .Refresh(OpenSearch.Net.Refresh.WaitFor), ct);

        GarantirSucesso(resposta.IsValid, resposta.DebugInformation, $"indexar jogo '{jogo.Id}'");
    }

    public async Task IndexarLoteAsync(IEnumerable<JogoResponseDto> jogos, CancellationToken ct = default)
    {
        var documentos = jogos.Select(ParaDocumento).ToList();
        if (documentos.Count == 0)
            return;

        await GarantirIndiceAsync(ct);
        var resposta = await client.BulkAsync(request => request
            .IndexMany(documentos, (operation, document) => operation
                .Index(_indice)
                .Id(document.Id))
            .Refresh(OpenSearch.Net.Refresh.WaitFor), ct);

        GarantirSucesso(
            resposta.IsValid && !resposta.Errors,
            resposta.DebugInformation,
            $"indexar lote de {documentos.Count} jogos");
    }

    public async Task RemoverAsync(string id, CancellationToken ct = default)
    {
        await GarantirIndiceAsync(ct);
        var resposta = await client.DeleteAsync<JogoIndiceDocumento>(id, request => request
            .Index(_indice)
            .Refresh(OpenSearch.Net.Refresh.WaitFor), ct);

        GarantirSucesso(resposta.IsValid, resposta.DebugInformation, $"remover jogo '{id}' do índice");
    }

    public async Task LimparAsync(CancellationToken ct = default)
    {
        await GarantirIndiceAsync(ct);
        var resposta = await client.DeleteByQueryAsync<JogoIndiceDocumento>(request => request
            .Index(_indice)
            .Query(query => query.MatchAll())
            .Refresh(OpenSearch.Net.Refresh.WaitFor), ct);

        GarantirSucesso(resposta.IsValid, resposta.DebugInformation, $"limpar o índice '{_indice}'");
    }

    private static JogoIndiceDocumento ParaDocumento(JogoResponseDto jogo) =>
        new(jogo.Id, jogo.Titulo, jogo.Descricao, jogo.Genero.ToString(), jogo.Preco, jogo.Ativo);

    private static void GarantirSucesso(bool sucesso, string detalhes, string operacao)
    {
        if (!sucesso)
            throw new InvalidOperationException($"Falha ao {operacao} no OpenSearch. {detalhes}");
    }

    private sealed record JogoIndiceDocumento(
        [property: PropertyName("id")] string Id,
        [property: PropertyName("titulo")] string Titulo,
        [property: PropertyName("descricao")] string Descricao,
        [property: PropertyName("genero")] string Genero,
        [property: PropertyName("preco")] decimal Preco,
        [property: PropertyName("ativo")] bool Ativo);
}
