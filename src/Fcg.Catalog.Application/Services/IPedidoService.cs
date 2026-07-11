using Fcg.Catalog.Application.DTOs.Response;

namespace Fcg.Catalog.Application.Services;

public interface IPedidoService
{
    Task<PedidoResponseDto> ObterPorOrderIdAsync(string orderId, CancellationToken ct = default);
}
