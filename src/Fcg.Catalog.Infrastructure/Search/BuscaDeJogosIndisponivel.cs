using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Interfaces;

namespace Fcg.Catalog.Infrastructure.Search;

public sealed class BuscaDeJogosIndisponivel : IBuscaDeJogos
{
    public Task<IReadOnlyList<JogoBuscaResponseDto>> BuscarAsync(string termo, CancellationToken ct = default) =>
        Task.FromException<IReadOnlyList<JogoBuscaResponseDto>>(
            new InvalidOperationException(
                "A busca OpenSearch está indisponível. Configure OpenSearch:Endpoint para habilitá-la."));
}
