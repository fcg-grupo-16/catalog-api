using System.Security.Claims;
using Fcg.Catalog.Application.Services;
using Fcg.Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fcg.Catalog.Api.Controllers;

/// <summary>
/// Controller responsável pelo gerenciamento de pedidos.
/// </summary>
[ApiController]
[Route("api/v1/pedidos")]
[Produces("application/json")]
[Authorize(Policy = "UsuarioAutenticado")]
public sealed class PedidosController : Controller
{
    IPedidoService pedidoService;
    public PedidosController(IPedidoService _pedidoService)
    {
        pedidoService = _pedidoService;
    }

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> ObterPorId(string orderId, CancellationToken ct)
    {
        var pedido = await pedidoService.ObterPorOrderIdAsync(orderId, ct); 
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Administrador");
        if (!isAdmin && pedido.UserId != usuarioId) throw new AcessoNegadoException(); 
        return Ok(pedido);
    }
}
