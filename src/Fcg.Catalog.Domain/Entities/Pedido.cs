using Fcg.Catalog.Domain.Enums;


namespace Fcg.Catalog.Domain.Entities;

public class Pedido
{
    public string Id { get; private set; }
    public string OrderId { get; private set; }
    public string UserId { get; private set; }
    public string GameId { get; private set; }
    public decimal Price { get; private set; }
    public StatusPedido Status { get; private set; }
    public DateTime DataCriacao { get; private set; }
    public DateTime DataAtualizacao { get; private set; }

    public Pedido(string orderId, string userId, string gameId, decimal price)
    {
        Id = string.Empty;
        OrderId = orderId; UserId = userId; GameId = gameId; Price = price;
        Status = StatusPedido.Pending;
        DataCriacao = DataAtualizacao = DateTime.UtcNow;
    }
    private Pedido() { Id = string.Empty; UserId = string.Empty; GameId = string.Empty; }

    public void Aprovar() { Status = StatusPedido.Approved; DataAtualizacao = DateTime.UtcNow; }
    public void Rejeitar() { Status = StatusPedido.Rejected; DataAtualizacao = DateTime.UtcNow; }
}
