using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Interfaces;
using Fcg.Catalog.Domain.Exceptions;

namespace Fcg.Catalog.Infrastructure.Search;

public sealed class IndiceDeJogosIndisponivel : IIndiceDeJogos
{
    private const string Mensagem =
        "O índice OpenSearch está indisponível. Configure OpenSearch:Endpoint para habilitá-lo.";

    public Task GarantirIndiceAsync(CancellationToken ct = default) => Indisponivel();

    public Task IndexarAsync(JogoResponseDto jogo, CancellationToken ct = default) => Indisponivel();

    public Task IndexarLoteAsync(IEnumerable<JogoResponseDto> jogos, CancellationToken ct = default) => Indisponivel();

    public Task RemoverAsync(string id, CancellationToken ct = default) => Indisponivel();

    public Task RemoverExcetoAsync(IReadOnlyCollection<string> idsMantidos, CancellationToken ct = default) =>
        Indisponivel();

    private static Task Indisponivel() =>
        Task.FromException(new ServicoIndisponivelException(Mensagem));
}
