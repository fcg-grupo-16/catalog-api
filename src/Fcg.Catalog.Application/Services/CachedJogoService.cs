using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Interfaces;
using Fcg.Catalog.Domain.Enums;

namespace Fcg.Catalog.Application.Services;

public sealed class CachedJogoService(IJogoService inner, ICacheService cache) : IJogoService
{
    private const string GrupoJogos = "jogos";
    private static readonly TimeSpan TtlLista = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan TtlJogo = TimeSpan.FromMinutes(10);

    public async Task<JogoResponseDto> CriarAsync(CriarJogoRequestDto dto, CancellationToken ct = default)
    {
        var criado = await inner.CriarAsync(dto, ct);
        await cache.InvalidateGroupAsync(GrupoJogos, ct);
        return criado;
    }

    public async Task<IReadOnlyList<JogoResponseDto>> InserirLoteAsync(List<CriarJogoRequestDto> listaDto, CancellationToken ct = default)
    {
        var criados = await inner.InserirLoteAsync(listaDto, ct);
        await cache.InvalidateGroupAsync(GrupoJogos, ct);
        return criados;
    }

    public async Task<JogoResponseDto> ObterPorIdAsync(string id, CancellationToken ct = default)
    {
        var key = $"jogo:{id}";
        var cached = await cache.GetAsync<JogoResponseDto>(key, ct);
        if (cached is not null)
            return cached;

        var jogo = await inner.ObterPorIdAsync(id, ct);
        await cache.SetAsync(key, jogo, TtlJogo, ct);
        return jogo;
    }

    public async Task<PaginacaoResponseDto<JogoResponseDto>> ListarAsync(int pagina, int tamanhoPagina, GeneroJogo? genero, CancellationToken ct = default)
    {
        var generation = await cache.GetGenerationAsync(GrupoJogos, ct);
        var generoKey = genero?.ToString() ?? "todos";
        var key = $"jogos:lista:g{generation}:p{pagina}:t{tamanhoPagina}:gen{generoKey}";

        var cached = await cache.GetAsync<PaginacaoResponseDto<JogoResponseDto>>(key, ct);
        if (cached is not null)
            return cached;

        var dto = await inner.ListarAsync(pagina, tamanhoPagina, genero, ct);
        await cache.SetAsync(key, dto, TtlLista, ct);
        return dto;
    }

    public async Task<JogoResponseDto> AtualizarAsync(string id, AtualizarJogoRequestDto dto, CancellationToken ct = default)
    {
        var atualizado = await inner.AtualizarAsync(id, dto, ct);
        await cache.RemoveAsync($"jogo:{id}", ct);
        await cache.InvalidateGroupAsync(GrupoJogos, ct);
        return atualizado;
    }

    public async Task RemoverAsync(string id, CancellationToken ct = default)
    {
        await inner.RemoverAsync(id, ct);
        await cache.RemoveAsync($"jogo:{id}", ct);
        await cache.InvalidateGroupAsync(GrupoJogos, ct);
    }
}
