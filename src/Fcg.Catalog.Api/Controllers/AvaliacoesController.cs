using System.Security.Claims;
using Fcg.Catalog.Api.Extensions;
using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Services;
using Fcg.Catalog.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fcg.Catalog.Api.Controllers;

/// <summary>
/// Controller responsável pelas avaliações de jogos.
/// </summary>
[ApiController]
[Route("api/v1/avaliacoes")]
[Produces("application/json")]
[Authorize(Policy = "UsuarioAutenticado")]
public sealed class AvaliacoesController(
    IAvaliacaoService avaliacaoService,
    IValidator<CriarAvaliacaoRequestDto> criarAvaliacaoValidator) : ControllerBase
{
    private string UsuarioIdAtual =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new AcessoNegadoException("Token sem identificação de usuário.");

    private bool EhAdmin => User.IsInRole("Administrador");

    /// <summary>
    /// Cria uma avaliação para um jogo.
    /// </summary>
    /// <param name="dto">Dados da avaliação.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Dados da avaliação criada.</returns>
    /// <response code="201">Avaliação criada com sucesso.</response>
    /// <response code="404">Jogo não encontrado.</response>
    /// <response code="409">Usuário já avaliou este jogo.</response>
    /// <response code="422">Dados inválidos.</response>
    [HttpPost]
    [ProducesResponseType(typeof(AvaliacaoResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarAvaliacaoRequestDto dto, CancellationToken ct)
    {
        await criarAvaliacaoValidator.ValidarAsync(dto, ct);

        var usuarioId = UsuarioIdAtual;
        var criada = await avaliacaoService.CriarAsync(usuarioId, dto, ct);

        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id }, criada);
    }

    /// <summary>
    /// Obtém uma avaliação por ID.
    /// </summary>
    /// <param name="id">Identificador da avaliação.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Dados da avaliação.</returns>
    /// <response code="200">Avaliação encontrada.</response>
    /// <response code="404">Avaliação não encontrada.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AvaliacaoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(string id, CancellationToken ct)
    {
        var resultado = await avaliacaoService.ObterPorIdAsync(id, ct);
        return Ok(resultado);
    }

    /// <summary>
    /// Lista avaliações de um jogo em ordem decrescente de data de criação.
    /// </summary>
    /// <param name="jogoId">Identificador do jogo.</param>
    /// <param name="pagina">Número da página.</param>
    /// <param name="tamanhoPagina">Quantidade de itens por página.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Feed paginado de avaliações.</returns>
    [HttpGet("/api/v1/jogos/{jogoId}/avaliacoes")]
    [ProducesResponseType(typeof(PaginacaoResponseDto<AvaliacaoResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarPorJogo(
        string jogoId,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 10,
        CancellationToken ct = default)
    {
        var resultado = await avaliacaoService.ListarPorJogoAsync(jogoId, pagina, tamanhoPagina, ct);
        return Ok(resultado);
    }

    /// <summary>
    /// Obtém o resumo agregando total, média e distribuição de notas para um jogo.
    /// </summary>
    /// <param name="jogoId">Identificador do jogo.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Resumo agregado das avaliações.</returns>
    [HttpGet("/api/v1/jogos/{jogoId}/avaliacoes/resumo")]
    [ProducesResponseType(typeof(AvaliacaoResumoResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterResumo(string jogoId, CancellationToken ct)
    {
        var resultado = await avaliacaoService.ObterResumoAsync(jogoId, ct);
        return Ok(resultado);
    }

    /// <summary>
    /// Incrementa o contador de votos úteis de uma avaliação.
    /// </summary>
    /// <param name="id">Identificador da avaliação.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Atualização do contador.</returns>
    /// <response code="200">Voto útil registrado.</response>
    /// <response code="404">Avaliação não encontrada.</response>
    [HttpPost("{id}/util")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IncrementarVotoUtil(string id, CancellationToken ct)
    {
        var novoTotal = await avaliacaoService.IncrementarVotoUtilAsync(id, ct);

        if (novoTotal is null)
        {
            return NotFound();
        }

        return Ok(novoTotal.Value);
    }

    /// <summary>
    /// Remove uma avaliação.
    /// </summary>
    /// <param name="id">Identificador da avaliação.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <response code="204">Avaliação removida.</response>
    /// <response code="403">Usuário não autorizado.</response>
    /// <response code="404">Avaliação não encontrada.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(string id, CancellationToken ct)
    {
        await avaliacaoService.RemoverAsync(id, UsuarioIdAtual, EhAdmin, ct);
        return NoContent();
    }
}
