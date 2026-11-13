using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Interfaces;
using Fcg.Catalog.Domain.Exceptions;

namespace Fcg.Catalog.Infrastructure.Search;

public sealed class BuscaDeJogosIndisponivel : IBuscaDeJogos
{
    public Task<IReadOnlyList<JogoBuscaResponseDto>> BuscarAsync(string termo, CancellationToken ct = default) =>
        Task.FromException<IReadOnlyList<JogoBuscaResponseDto>>(
            new ServicoIndisponivelException(
                "A busca OpenSearch está indisponível. Configure OpenSearch:Endpoint para habilitá-la."));
}
