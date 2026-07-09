using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Domain.Entities;

namespace Fcg.Catalog.Application.Services;

public interface IPedidoService
{
    Task<PedidoResponseDto?> ObterPorOrderIdAsync(string orderId, CancellationToken ct = default);
}
