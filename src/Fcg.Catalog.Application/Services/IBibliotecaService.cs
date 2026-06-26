using Fcg.Catalog.Application.DTOs.Response;

namespace Fcg.Catalog.Application.Services;

public interface IBibliotecaService
{
    Task<IEnumerable<JogoResponseDto>> ListarBibliotecaAsync(string usuarioId, CancellationToken ct = default);
}
