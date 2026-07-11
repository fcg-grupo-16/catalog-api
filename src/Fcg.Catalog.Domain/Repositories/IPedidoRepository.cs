using Fcg.Catalog.Domain.Entities;

namespace Fcg.Catalog.Domain.Repositories;

public interface IPedidoRepository
{
    /// <summary>Adiciona o pedido ao contexto SEM persistir — o commit ocorre em
    /// <see cref="SalvarAlteracoesAsync"/>, permitindo que a gravação do pedido e a
    /// publicação do evento (outbox transacional) sejam atômicas.</summary>
    Task AdicionarSemSalvarAsync(Pedido pedido, CancellationToken ct = default);

    /// <summary>Persiste as alterações pendentes do contexto (entidade + mensagens do outbox).</summary>
    Task SalvarAlteracoesAsync(CancellationToken ct = default);

    Task<Pedido?> ObterPorOrderIdAsync(string orderId, CancellationToken ct = default);
    Task AtualizarAsync(Pedido pedido, CancellationToken ct = default);
}
