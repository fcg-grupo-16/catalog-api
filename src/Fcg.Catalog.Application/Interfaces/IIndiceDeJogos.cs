using Fcg.Catalog.Application.DTOs.Response;

namespace Fcg.Catalog.Application.Interfaces;

public interface IIndiceDeJogos
{
    Task GarantirIndiceAsync(CancellationToken ct = default);
    Task IndexarAsync(JogoResponseDto jogo, CancellationToken ct = default);
    Task IndexarLoteAsync(IEnumerable<JogoResponseDto> jogos, CancellationToken ct = default);
    Task RemoverAsync(string id, CancellationToken ct = default);
    Task LimparAsync(CancellationToken ct = default);
}
