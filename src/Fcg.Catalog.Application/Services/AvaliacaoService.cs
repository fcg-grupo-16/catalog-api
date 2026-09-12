using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Exceptions;
using Fcg.Catalog.Domain.Repositories;

namespace Fcg.Catalog.Application.Services;

public sealed class AvaliacaoService(
    IAvaliacaoRepository avaliacaoRepository,
    IJogoRepository jogoRepository) : IAvaliacaoService
{
    public async Task<AvaliacaoResponseDto> CriarAsync(
        string usuarioId, CriarAvaliacaoRequestDto dto, CancellationToken ct = default)
    {
        _ = await jogoRepository.ObterPorIdAsync(dto.JogoId, ct)
            ?? throw new EntidadeNaoEncontradaException("Jogo", dto.JogoId);

        var avaliacao = new Avaliacao(dto.JogoId, usuarioId, dto.Nota, dto.Comentario, dto.Titulo, dto.Tags, dto.Contexto);
        var criada = await avaliacaoRepository.CriarAsync(avaliacao, ct);
        return MapToDto(criada);
    }

    public async Task<PaginacaoResponseDto<AvaliacaoResponseDto>> ListarPorJogoAsync(
        string jogoId, int pagina, int tamanhoPagina, CancellationToken ct = default)
    {
        var avaliacoes = await avaliacaoRepository.ListarPorJogoAsync(jogoId, pagina, tamanhoPagina, ct);
        // O total vem do ContarPorJogoAsync. Sem ele o cliente não sabe quantas páginas existem —
        // e o método ficava implementado no repositório sem nenhum chamador. O contrato agora
        // acompanha o de /api/v1/jogos, que já devolve Total.
        var total = await avaliacaoRepository.ContarPorJogoAsync(jogoId, ct);

        return new PaginacaoResponseDto<AvaliacaoResponseDto>(
            avaliacoes.Select(MapToDto).ToList(), pagina, tamanhoPagina, total);
    }

    public async Task<AvaliacaoResumoResponseDto> ObterResumoAsync(string jogoId, CancellationToken ct = default)
    {
        var resumo = await avaliacaoRepository.ObterResumoAsync(jogoId, ct);
        return MapToDto(resumo);
    }

    public async Task<AvaliacaoResponseDto> ObterPorIdAsync(string id, CancellationToken ct = default)
    {
        var avaliacao = await avaliacaoRepository.ObterPorIdAsync(id, ct)
            ?? throw new EntidadeNaoEncontradaException("Avaliação", id);

        return MapToDto(avaliacao);
    }

    public async Task<int?> IncrementarVotoUtilAsync(string id, CancellationToken ct = default)
    {
        var avaliacaoExiste = await avaliacaoRepository.ObterPorIdAsync(id, ct);
        if (avaliacaoExiste is null)
        {
            return null;
        }

        return await avaliacaoRepository.IncrementarVotoUtilAsync(id, ct);
    }

    public async Task RemoverAsync(string id, string usuarioId, bool ehAdmin, CancellationToken ct = default)
    {
        var avaliacao = await avaliacaoRepository.ObterPorIdAsync(id, ct)
            ?? throw new EntidadeNaoEncontradaException("Avaliação", id);

        if (!ehAdmin && avaliacao.UsuarioId != usuarioId)
            throw new AcessoNegadoException("Só o autor da avaliação ou um administrador pode removê-la.");

        await avaliacaoRepository.RemoverAsync(id, ct);
    }

    private static AvaliacaoResponseDto MapToDto(Avaliacao avaliacao) =>
        new(
            avaliacao.Id,
            avaliacao.JogoId,
            avaliacao.UsuarioId,
            avaliacao.Nota,
            avaliacao.Titulo,
            avaliacao.Comentario,
            avaliacao.Tags,
            avaliacao.Contexto,
            avaliacao.VotosUteis,
            avaliacao.DataCriacao);

    private static AvaliacaoResumoResponseDto MapToDto(AvaliacaoResumo resumo) =>
        new(
            resumo.JogoId,
            resumo.Total,
            resumo.MediaNota,
            resumo.Distribuicao);
}
