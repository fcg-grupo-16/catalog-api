using Fcg.Catalog.Domain.Entities;

namespace Fcg.Catalog.Domain.Repositories;

public interface IPedidoRepository
{
    Task CriarAsync(Pedido pedido, CancellationToken ct = default);
    Task<Pedido?> ObterPorOrderIdAsync(string orderId, CancellationToken ct = default);
    Task AtualizarAsync(Pedido pedido, CancellationToken ct = default);
}
