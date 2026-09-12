using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.DTOs.Response;

namespace Fcg.Catalog.Application.Services;

public interface IAvaliacaoService
{
    Task<AvaliacaoResponseDto> CriarAsync(string usuarioId, CriarAvaliacaoRequestDto dto, CancellationToken ct = default);
    Task<PaginacaoResponseDto<AvaliacaoResponseDto>> ListarPorJogoAsync(string jogoId, int pagina, int tamanhoPagina, CancellationToken ct = default);
    Task<AvaliacaoResumoResponseDto> ObterResumoAsync(string jogoId, CancellationToken ct = default);
    Task<AvaliacaoResponseDto> ObterPorIdAsync(string id, CancellationToken ct = default);
    Task<int?> IncrementarVotoUtilAsync(string id, CancellationToken ct = default);
    Task RemoverAsync(string id, string usuarioId, bool ehAdmin, CancellationToken ct = default);
}
