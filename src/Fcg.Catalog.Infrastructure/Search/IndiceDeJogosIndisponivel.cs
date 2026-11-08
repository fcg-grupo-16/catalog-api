using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Interfaces;

namespace Fcg.Catalog.Infrastructure.Search;

public sealed class IndiceDeJogosIndisponivel : IIndiceDeJogos
{
    private const string Mensagem =
        "O índice OpenSearch está indisponível. Configure OpenSearch:Endpoint para habilitá-lo.";

    public Task GarantirIndiceAsync(CancellationToken ct = default) => Indisponivel();

    public Task IndexarAsync(JogoResponseDto jogo, CancellationToken ct = default) => Indisponivel();

    public Task IndexarLoteAsync(IEnumerable<JogoResponseDto> jogos, CancellationToken ct = default) => Indisponivel();

    public Task RemoverAsync(string id, CancellationToken ct = default) => Indisponivel();

    public Task LimparAsync(CancellationToken ct = default) => Indisponivel();

    private static Task Indisponivel() =>
        Task.FromException(new InvalidOperationException(Mensagem));
}
