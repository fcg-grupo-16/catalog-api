using Fcg.Catalog.Domain.Entities;

namespace Fcg.Catalog.Domain.Repositories;

public interface IAvaliacaoRepository
{
    /// <summary>Insere a avaliação. Lança <see cref="Exceptions.ConflitoDeDadosException"/> se o usuário já avaliou o jogo.</summary>
    Task<Avaliacao> CriarAsync(Avaliacao avaliacao, CancellationToken ct = default);

    /// <summary>Feed paginado de um jogo, mais recentes primeiro.</summary>
    Task<IReadOnlyList<Avaliacao>> ListarPorJogoAsync(string jogoId, int pagina, int tamanhoPagina, CancellationToken ct = default);

    Task<long> ContarPorJogoAsync(string jogoId, CancellationToken ct = default);

    /// <summary>Agregação de média, total e distribuição de notas — um único round-trip.</summary>
    Task<AvaliacaoResumo> ObterResumoAsync(string jogoId, CancellationToken ct = default);

    Task<Avaliacao?> ObterPorIdAsync(string id, CancellationToken ct = default);

    /// <summary>Incremento atômico do contador de votos úteis. Devolve o total novo, ou <c>null</c> se não existir.</summary>
    Task<int?> IncrementarVotoUtilAsync(string id, CancellationToken ct = default);

    Task<bool> RemoverAsync(string id, CancellationToken ct = default);

    /// <summary>Cria os índices da collection. Idempotente — chamada no startup.</summary>
    Task GarantirIndicesAsync(CancellationToken ct = default);
}