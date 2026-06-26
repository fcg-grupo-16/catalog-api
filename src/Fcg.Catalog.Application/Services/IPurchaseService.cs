namespace Fcg.Catalog.Application.Services;

public interface IPurchaseService
{
    Task IniciarCompraAsync(string usuarioId, string jogoId, CancellationToken ct = default);
}
