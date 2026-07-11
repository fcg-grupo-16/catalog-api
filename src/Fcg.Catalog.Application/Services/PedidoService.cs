using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Exceptions;
using Fcg.Catalog.Domain.Repositories;

namespace Fcg.Catalog.Application.Services;

public sealed class PedidoService(IPedidoRepository pedidoRepository) : IPedidoService
{
    public async Task<PedidoResponseDto> ObterPorOrderIdAsync(string orderId, CancellationToken ct = default)
    {
        Pedido pedido = await pedidoRepository.ObterPorOrderIdAsync(orderId, ct)
            ?? throw new EntidadeNaoEncontradaException("Pedido", orderId);

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
