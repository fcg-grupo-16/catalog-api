using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Domain.Enums;

namespace Fcg.Catalog.Application.Services;

public interface IJogoService
{
    Task<JogoResponseDto> CriarAsync(CriarJogoRequestDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<JogoResponseDto>> InserirLoteAsync(List<CriarJogoRequestDto> listaDto, CancellationToken ct = default);
    Task<JogoResponseDto> ObterPorIdAsync(string id, CancellationToken ct = default);
    Task<PaginacaoResponseDto<JogoResponseDto>> ListarAsync(int pagina, int tamanhoPagina, GeneroJogo? genero, CancellationToken ct = default);
    Task<JogoResponseDto> AtualizarAsync(string id, AtualizarJogoRequestDto dto, CancellationToken ct = default);
    Task RemoverAsync(string id, CancellationToken ct = default);
}
