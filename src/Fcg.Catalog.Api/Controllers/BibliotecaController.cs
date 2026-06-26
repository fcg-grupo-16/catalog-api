using System.Security.Claims;
using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Services;
using Fcg.Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fcg.Catalog.Api.Controllers;

/// <summary>
/// Controller responsável pela biblioteca de jogos do usuário.
/// </summary>
[ApiController]
[Route("api/v1/biblioteca")]
[Produces("application/json")]
[Authorize(Policy = "UsuarioAutenticado")]
public sealed class BibliotecaController(
    IBibliotecaService bibliotecaService,
    IPurchaseService purchaseService) : ControllerBase
{
    /// <summary>
    /// Listar jogos da biblioteca do usuário autenticado.
    /// </summary>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Lista de jogos adquiridos.</returns>
    /// <response code="200">Biblioteca retornada com sucesso.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<JogoResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var usuarioId = ObterUsuarioId();

        var resultado = await bibliotecaService.ListarBibliotecaAsync(usuarioId, ct);

        return Ok(resultado);
    }

    /// <summary>
    /// Iniciar a compra de um jogo para a biblioteca do usuário autenticado.
    /// O processamento é assíncrono: publica um <c>OrderPlacedEvent</c> e a biblioteca
    /// é atualizada quando o pagamento for aprovado.
    /// </summary>
    /// <param name="dto">Dados do jogo a adquirir.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <response code="202">Compra iniciada e aceita para processamento.</response>
    /// <response code="404">Jogo não encontrado.</response>
    /// <response code="409">Jogo já adquirido.</response>
    /// <response code="422">Jogo inativo.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Adquirir([FromBody] AdquirirJogoRequestDto dto, CancellationToken ct)
    {
        var usuarioId = ObterUsuarioId();

        await purchaseService.IniciarCompraAsync(usuarioId, dto.JogoId, ct);

        return Accepted();
    }

    private string ObterUsuarioId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new AcessoNegadoException();
}
