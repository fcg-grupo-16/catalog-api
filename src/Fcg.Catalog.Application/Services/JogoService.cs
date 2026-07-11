using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Enums;
using Fcg.Catalog.Domain.Exceptions;
using Fcg.Catalog.Domain.Repositories;
using Fcg.Catalog.Domain.ValueObjects;

namespace Fcg.Catalog.Application.Services;

public sealed class JogoService(IJogoRepository jogoRepository) : IJogoService
{
    public async Task<JogoResponseDto> CriarAsync(CriarJogoRequestDto dto, CancellationToken ct = default)
    {
        if (await jogoRepository.TituloExisteAsync(dto.Titulo, ct))
        {
            throw new ConflitoDeDadosException("Jogo", "título", dto.Titulo);
        }

        var preco = new Preco(dto.Preco);
        var jogo = new Jogo(dto.Titulo, dto.Descricao, dto.Genero, preco, dto.DataLancamento);

        await jogoRepository.CriarAsync(jogo, ct);

        return MapToDto(jogo);
    }



    public async Task<IReadOnlyList<JogoResponseDto>> InserirLoteAsync(List<CriarJogoRequestDto> listaDto, CancellationToken ct = default)
    {
        // Verificação de duplicidade em uma única consulta (evita N+1) e dedup dentro do lote.
        var titulos = listaDto.Select(d => d.Titulo).ToList();
        var existentes = (await jogoRepository.TitulosExistentesAsync(titulos, ct)).ToHashSet();

        var novos = listaDto
            .Where(d => !existentes.Contains(d.Titulo))
            .GroupBy(d => d.Titulo)
            .Select(g => g.First())
            .Select(d => new Jogo(d.Titulo, d.Descricao, d.Genero, new Preco(d.Preco), d.DataLancamento))
            .ToList();

        if (novos.Count < 1)
        {
            throw new ConflitoDeDadosException("Todos os jogos da lista já estão cadastrados na base de dados.");
        }

        await jogoRepository.CriarLote(novos, ct);

        return novos.Select(MapToDto).ToList();
    }


    public async Task<JogoResponseDto> ObterPorIdAsync(string id, CancellationToken ct = default)
    {
        var jogo = await jogoRepository.ObterPorIdAsync(id, ct)
            ?? throw new EntidadeNaoEncontradaException("Jogo", id);

        return MapToDto(jogo);
    }

    public async Task<PaginacaoResponseDto<JogoResponseDto>> ListarAsync(int pagina, int tamanhoPagina, GeneroJogo? genero, CancellationToken ct = default)
    {
        IEnumerable<Jogo> jogos = genero.HasValue
            ? await jogoRepository.BuscarPorGeneroAsync(genero.Value, pagina, tamanhoPagina, ct)
            : await jogoRepository.ObterTodosAsync(pagina, tamanhoPagina, ct);

        int contagem = genero.HasValue
            ? await jogoRepository.ContagemTotalJogosGenero(genero.Value, ct)
            : await jogoRepository.ContagemTotalJogos(ct);

        var itens = jogos.Select(MapToDto).ToList();

        return new PaginacaoResponseDto<JogoResponseDto>(itens, pagina, tamanhoPagina, contagem);
    }

    public async Task<JogoResponseDto> AtualizarAsync(string id, AtualizarJogoRequestDto dto, CancellationToken ct = default)
    {
        var jogo = await jogoRepository.ObterPorIdAsync(id, ct)
            ?? throw new EntidadeNaoEncontradaException("Jogo", id);

        if (dto.Titulo != jogo.Titulo && await jogoRepository.TituloExisteAsync(dto.Titulo, ct))
        {
            throw new ConflitoDeDadosException("Jogo", "título", dto.Titulo);
        }

        var novoPreco = new Preco(dto.Preco);

        jogo.AtualizarTitulo(dto.Titulo);
        jogo.AtualizarDescricao(dto.Descricao);
        jogo.AtualizarGenero(dto.Genero);
        jogo.AtualizarPreco(novoPreco);

        await jogoRepository.AtualizarAsync(jogo, ct);

        return MapToDto(jogo);
    }

    public async Task RemoverAsync(string id, CancellationToken ct = default)
    {
        var jogo = await jogoRepository.ObterPorIdAsync(id, ct)
            ?? throw new EntidadeNaoEncontradaException("Jogo", id);

        jogo.Desativar();
        await jogoRepository.AtualizarAsync(jogo, ct);
    }

    private static JogoResponseDto MapToDto(Jogo jogo) =>
        new(
            jogo.Id,
            jogo.Titulo,
            jogo.Descricao,
            jogo.Genero,
            jogo.Preco.Valor,
            jogo.Preco.Moeda,
            jogo.DataLancamento,
            jogo.Ativo);
}
