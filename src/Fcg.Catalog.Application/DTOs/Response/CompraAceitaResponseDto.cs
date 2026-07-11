namespace Fcg.Catalog.Application.DTOs.Response;

/// <summary>
/// Retorno da requisição de compra (202 Accepted): identifica o pedido criado
/// para que o cliente possa consultar seu status em <c>GET /api/v1/pedidos/{orderId}</c>.
/// </summary>
public sealed record CompraAceitaResponseDto(string OrderId);
