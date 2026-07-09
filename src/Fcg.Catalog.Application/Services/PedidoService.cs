using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Repositories;

namespace Fcg.Catalog.Application.Services;

public class PedidoService(IPedidoRepository pedidoRepository) : IPedidoService
{
    public async Task<PedidoResponseDto?> ObterPorOrderIdAsync(string orderId, CancellationToken ct = default)
    {
        Pedido? pedido = await pedidoRepository.ObterPorOrderIdAsync(orderId, ct);

        if (pedido == null)
            throw new InvalidOperationException($"Objeto com id {orderId} não encontrado");

        return MapToDto(pedido);

    }

    private static PedidoResponseDto MapToDto(Pedido pedido) =>
    new(
        pedido.OrderId,
        pedido.UserId,
        pedido.GameId,
        pedido.Price,
        pedido.Status.ToString(),
        pedido.DataCriacao,
        pedido.DataAtualizacao);

}
