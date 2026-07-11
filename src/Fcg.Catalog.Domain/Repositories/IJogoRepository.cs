using System.ComponentModel;
using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Enums;

namespace Fcg.Catalog.Domain.Repositories;

public interface IJogoRepository
{
    Task<Jogo?> ObterPorIdAsync(string id, CancellationToken ct = default);
    Task<IEnumerable<Jogo>> ObterTodosAsync(int pagina, int tamanhoPagina, CancellationToken ct = default);
    Task<IEnumerable<Jogo>> BuscarPorGeneroAsync(GeneroJogo genero, int pagina, int tamanhoPagina, CancellationToken ct = default);
    Task CriarAsync(Jogo jogo, CancellationToken ct = default);
    Task CriarLote(IEnumerable<Jogo> jogos, CancellationToken ct = default);
    Task AtualizarAsync(Jogo jogo, CancellationToken ct = default);
    Task RemoverAsync(string id, CancellationToken ct = default);
    Task<bool> TituloExisteAsync(string titulo, CancellationToken ct = default);
    Task<int> ContagemTotalJogos(CancellationToken ct = default);
    Task<int> ContagemTotalJogosGenero(GeneroJogo genero, CancellationToken ct = default);
}
