using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Domain.Repositories;

namespace Fcg.Catalog.Application.Services;

public sealed class BibliotecaService(
    IBibliotecaRepository bibliotecaRepository,
    IJogoRepository jogoRepository) : IBibliotecaService
{
    public async Task<IEnumerable<JogoResponseDto>> ListarBibliotecaAsync(string usuarioId, CancellationToken ct = default)
    {
        var bibliotecaJogos = await bibliotecaRepository.ObterPorUsuarioAsync(usuarioId, ct);

        var jogos = new List<JogoResponseDto>();
        foreach (var item in bibliotecaJogos)
        {
            var jogo = await jogoRepository.ObterPorIdAsync(item.JogoId, ct);
            if (jogo is not null)
            {
                jogos.Add(new JogoResponseDto(
                    jogo.Id,
                    jogo.Titulo,
                    jogo.Descricao,
                    jogo.Genero,
                    jogo.Preco.Valor,
                    jogo.Preco.Moeda,
                    jogo.DataLancamento,
                    jogo.Ativo));
            }
        }

        return jogos;
    }
}
