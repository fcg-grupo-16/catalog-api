namespace Fcg.Catalog.Application.Services;

public interface IPurchaseService
{
    Task<string> IniciarCompraAsync(string usuarioId, string jogoId, CancellationToken ct = default);
}
