using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Interfaces;
using Fcg.Catalog.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fcg.Catalog.Application.Services;

public sealed class IndexedJogoService(
    IJogoService inner,
    IIndiceDeJogos indice,
    ILogger<IndexedJogoService> logger) : IJogoService
{
    public async Task<JogoResponseDto> CriarAsync(CriarJogoRequestDto dto, CancellationToken ct = default)
    {
        var criado = await inner.CriarAsync(dto, ct);
        await TentarAtualizarIndiceAsync(
            () => indice.IndexarAsync(criado, ct),
            $"Jogo {criado.Id} salvo no catálogo, mas não foi indexado no OpenSearch.",
            ct);
        return criado;
    }

    public async Task<IReadOnlyList<JogoResponseDto>> InserirLoteAsync(
        List<CriarJogoRequestDto> listaDto,
        CancellationToken ct = default)
    {
        var criados = await inner.InserirLoteAsync(listaDto, ct);
        await TentarAtualizarIndiceAsync(
            () => indice.IndexarLoteAsync(criados, ct),
            $"Lote de {criados.Count} jogos salvo no catálogo, mas não foi indexado no OpenSearch.",
            ct);
        return criados;
    }

    public Task<JogoResponseDto> ObterPorIdAsync(string id, CancellationToken ct = default) =>
        inner.ObterPorIdAsync(id, ct);

    public Task<PaginacaoResponseDto<JogoResponseDto>> ListarAsync(
        int pagina,
        int tamanhoPagina,
        GeneroJogo? genero,
        CancellationToken ct = default) =>
        inner.ListarAsync(pagina, tamanhoPagina, genero, ct);

    public async Task<JogoResponseDto> AtualizarAsync(
        string id,
        AtualizarJogoRequestDto dto,
        CancellationToken ct = default)
    {
        var atualizado = await inner.AtualizarAsync(id, dto, ct);
        await TentarAtualizarIndiceAsync(
            () => indice.IndexarAsync(atualizado, ct),
            $"Jogo {atualizado.Id} atualizado no catálogo, mas não foi atualizado no OpenSearch.",
            ct);
        return atualizado;
    }

    public async Task RemoverAsync(string id, CancellationToken ct = default)
    {
        await inner.RemoverAsync(id, ct);
        await TentarAtualizarIndiceAsync(
            () => indice.RemoverAsync(id, ct),
            $"Jogo {id} desativado no catálogo, mas não foi removido do OpenSearch.",
            ct);
    }

    private async Task TentarAtualizarIndiceAsync(Func<Task> atualizar, string mensagem, CancellationToken ct)
    {
        try
        {
            await atualizar();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Mensagem}", mensagem);
        }
    }
}
