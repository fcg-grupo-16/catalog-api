using Fcg.Catalog.Domain.Entities;

namespace Fcg.Catalog.Domain.Repositories;

public interface IBibliotecaRepository
{
    Task<IEnumerable<BibliotecaJogo>> ObterPorUsuarioAsync(string usuarioId, CancellationToken ct = default);
    Task AdicionarJogoAsync(BibliotecaJogo bibliotecaJogo, CancellationToken ct = default);
    Task<bool> UsuarioPossuiJogoAsync(string usuarioId, string jogoId, CancellationToken ct = default);
}
