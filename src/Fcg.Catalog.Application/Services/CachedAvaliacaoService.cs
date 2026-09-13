using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Interfaces;

namespace Fcg.Catalog.Application.Services;

public sealed class CachedAvaliacaoService(IAvaliacaoService inner, ICacheService cache) : IAvaliacaoService
{
    private static readonly TimeSpan TtlResumo = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan TtlLista = TimeSpan.FromSeconds(30);

    private static string GrupoDoJogo(string jogoId) => $"avaliacoes:{jogoId}";

    public async Task<AvaliacaoResponseDto> CriarAsync(string usuarioId, CriarAvaliacaoRequestDto dto, CancellationToken ct = default)
    {
        var criada = await inner.CriarAsync(usuarioId, dto, ct);
        await InvalidarJogoAsync(dto.JogoId, ct);
        return criada;
    }

    public async Task<PaginacaoResponseDto<AvaliacaoResponseDto>> ListarPorJogoAsync(string jogoId, int pagina, int tamanhoPagina, CancellationToken ct = default)
    {
        var generation = await cache.GetGenerationAsync(GrupoDoJogo(jogoId), ct);
        var key = $"avaliacoes:lista:g{generation}:{jogoId}:p{pagina}:t{tamanhoPagina}";

        var cached = await cache.GetAsync<PaginacaoResponseDto<AvaliacaoResponseDto>>(key, ct);
        if (cached is not null)
            return cached;

        var lista = await inner.ListarPorJogoAsync(jogoId, pagina, tamanhoPagina, ct);
        await cache.SetAsync(key, lista, TtlLista, ct);
        return lista;
    }

    public async Task<AvaliacaoResumoResponseDto> ObterResumoAsync(string jogoId, CancellationToken ct = default)
    {
        var key = $"avaliacoes:resumo:{jogoId}";
        var cached = await cache.GetAsync<AvaliacaoResumoResponseDto>(key, ct);
        if (cached is not null)
            return cached;

        var resumo = await inner.ObterResumoAsync(jogoId, ct);
        await cache.SetAsync(key, resumo, TtlResumo, ct);
        return resumo;
    }

    public async Task<AvaliacaoResponseDto> ObterPorIdAsync(string id, CancellationToken ct = default)
        => await inner.ObterPorIdAsync(id, ct);

    public async Task<int?> IncrementarVotoUtilAsync(string id, CancellationToken ct = default)
    {
        var avaliacao = await inner.ObterPorIdAsync(id, ct);
        var total = await inner.IncrementarVotoUtilAsync(id, ct);

        if (total is not null && avaliacao is not null)
        {
            await cache.InvalidateGroupAsync(GrupoDoJogo(avaliacao.JogoId), ct);
        }

        return total;
    }

    public async Task RemoverAsync(string id, string usuarioId, bool ehAdmin, CancellationToken ct = default)
    {
        var avaliacao = await inner.ObterPorIdAsync(id, ct);
        await inner.RemoverAsync(id, usuarioId, ehAdmin, ct);
        await InvalidarJogoAsync(avaliacao.JogoId, ct);
    }

    private async Task InvalidarJogoAsync(string jogoId, CancellationToken ct)
    {
        await cache.RemoveAsync($"avaliacoes:resumo:{jogoId}", ct);
        await cache.InvalidateGroupAsync(GrupoDoJogo(jogoId), ct);
    }
}
