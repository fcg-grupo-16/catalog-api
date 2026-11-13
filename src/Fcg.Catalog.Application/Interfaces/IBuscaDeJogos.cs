using Fcg.Catalog.Application.DTOs.Response;

namespace Fcg.Catalog.Application.Interfaces;

public interface IBuscaDeJogos
{
    Task<IReadOnlyList<JogoBuscaResponseDto>> BuscarAsync(string termo, CancellationToken ct = default);
}
