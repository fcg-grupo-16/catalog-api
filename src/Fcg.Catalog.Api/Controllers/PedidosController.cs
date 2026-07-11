using System.Security.Claims;
using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Services;
using Fcg.Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fcg.Catalog.Api.Controllers;

/// <summary>
/// Controller responsável pela consulta de pedidos.
/// </summary>
[ApiController]
[Route("api/v1/pedidos")]
[Produces("application/json")]
[Authorize(Policy = "UsuarioAutenticado")]
public sealed class PedidosController(IPedidoService pedidoService) : ControllerBase
{
    /// <summary>
    /// Obter um pedido por seu OrderId. O usuário só pode consultar os próprios pedidos
    /// (administradores podem consultar qualquer pedido).
    /// </summary>
    /// <param name="orderId">Identificador do pedido (OrderId).</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <response code="200">Pedido retornado com sucesso.</response>
    /// <response code="403">Pedido pertence a outro usuário.</response>
    /// <response code="404">Pedido não encontrado.</response>
    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(PedidoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid orderId, CancellationToken ct)
    {
        // Guid garante a representação canônica (minúsculas, formato "D") na consulta por string.
        var pedido = await pedidoService.ObterPorOrderIdAsync(orderId.ToString(), ct);

        var usuarioId = ObterUsuarioId();
        if (!User.IsInRole("Administrador") && pedido.UserId != usuarioId)
        {
            throw new AcessoNegadoException();
        }

        return Ok(pedido);
    }

    private string ObterUsuarioId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new AcessoNegadoException();
}
