using Fcg.Catalog.Api.Extensions;
using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Services;
using Fcg.Catalog.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fcg.Catalog.Api.Controllers;

/// <summary>
/// Controller responsável pelo gerenciamento de jogos.
/// </summary>
[ApiController]
[Route("api/v1/jogos")]
[Produces("application/json")]
[Authorize(Policy = "UsuarioAutenticado")]
public sealed class JogosController(
    IJogoService jogoService,
    IValidator<CriarJogoRequestDto> criarValidator,
    IValidator<List<CriarJogoRequestDto>> criarListValidator,
    IValidator<AtualizarJogoRequestDto> atualizarValidator) : ControllerBase
{
    /// <summary>
    /// Listar jogos com paginação e filtro opcional por gênero.
    /// </summary>
    /// <param name="pagina">Número da página (padrão: 1).</param>
    /// <param name="tamanhoPagina">Itens por página (padrão: 10).</param>
    /// <param name="genero">Filtro opcional por gênero.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Lista paginada de jogos.</returns>
    /// <response code="200">Lista de jogos retornada com sucesso.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PaginacaoResponseDto<JogoResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 10,
        [FromQuery] GeneroJogo? genero = null,
        CancellationToken ct = default)
    {
        var resultado = await jogoService.ListarAsync(pagina, tamanhoPagina, genero, ct);

        return Ok(resultado);
    }

    /// <summary>
    /// Obter jogo por ID.
    /// </summary>
    /// <param name="id">Identificador do jogo.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Dados do jogo.</returns>
    /// <response code="200">Jogo retornado com sucesso.</response>
    /// <response code="404">Jogo não encontrado.</response>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(JogoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(string id, CancellationToken ct)
    {
        var resultado = await jogoService.ObterPorIdAsync(id, ct);

        return Ok(resultado);
    }

    /// <summary>
    /// Cadastrar novo jogo.
    /// </summary>
    /// <param name="dto">Dados do novo jogo.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Jogo novo inserido na base.</returns>
    /// <response code="201">Jogo criado com sucesso.</response>
    /// <response code="409">Título já cadastrado.</response>
    /// <response code="422">Dados de validação inválidos.</response>
    [HttpPost]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(JogoResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarJogoRequestDto dto, CancellationToken ct)
    {
        await criarValidator.ValidarAsync(dto, ct);

        var resultado = await jogoService.CriarAsync(dto, ct);

        return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Id }, resultado);
    }

    /// <summary>
    /// Cadastrar novos jogos em lote.
    /// </summary>
    /// <param name="listaDto">Dados dos novos jogos a serem inseridos em lote.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Jogos novos inseridos na base.</returns>
    /// <response code="201">Jogos inseridos na base com sucesso.</response>
    /// <response code="409">Títulos já cadastrados.</response>
    /// <response code="422">Dados de validação inválidos.</response>
    [HttpPost("InserirLote")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<JogoResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> InserirLote([FromBody] List<CriarJogoRequestDto> listaDto, CancellationToken ct)
    {
        await criarListValidator.ValidarAsync(listaDto, ct);
        var criados = await jogoService.InserirLoteAsync(listaDto, ct);
        return StatusCode(StatusCodes.Status201Created, criados);
    }

    /// <summary>
    /// Atualizar dados do jogo.
    /// </summary>
    /// <param name="id">Identificador do jogo.</param>
    /// <param name="dto">Dados atualizados.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Jogo atualizado.</returns>
    /// <response code="200">Jogo atualizado com sucesso.</response>
    /// <response code="404">Jogo não encontrado.</response>
    /// <response code="409">Título já cadastrado.</response>
    /// <response code="422">Dados de validação inválidos.</response>
    [HttpPut("{id}")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(JogoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Atualizar(string id, [FromBody] AtualizarJogoRequestDto dto, CancellationToken ct)
    {
        await atualizarValidator.ValidarAsync(dto, ct);

        var resultado = await jogoService.AtualizarAsync(id, dto, ct);

        return Ok(resultado);
    }

    /// <summary>
    /// Desativar jogo (soft delete).
    /// </summary>
    /// <param name="id">Identificador do jogo.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <response code="204">Jogo desativado com sucesso.</response>
    /// <response code="404">Jogo não encontrado.</response>
    [HttpDelete("{id}")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(string id, CancellationToken ct)
    {
        await jogoService.RemoverAsync(id, ct);

        return NoContent();
    }
}
