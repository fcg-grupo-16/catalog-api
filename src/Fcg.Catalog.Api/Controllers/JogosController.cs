using Fcg.Catalog.Api.Extensions;
using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Interfaces;
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
    IBuscaDeJogos buscaService,
    IIndiceDeJogos indiceDeJogos,
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
    /// Buscar jogos por relevância, com tolerância a erros de digitação.
    /// </summary>
    /// <param name="q">Termo de busca.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Jogos encontrados ordenados por relevância.</returns>
    /// <response code="200">Resultados da busca, ou lista vazia quando não houver correspondência.</response>
    /// <response code="401">Autenticação ausente ou inválida.</response>
    /// <response code="503">OpenSearch não configurado ou fora do ar.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<JogoBuscaResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Buscar([FromQuery] string q, CancellationToken ct)
        => Ok(await buscaService.BuscarAsync(q, ct));

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
    /// Reconstruir o índice OpenSearch com os jogos ativos do catálogo.
    /// </summary>
    /// <remarks>
    /// Indexa todos os jogos ativos com `_bulk` (sobrescreve pelo id) e só DEPOIS remove do índice o
    /// que não está mais ativo. Na ordem inversa — apagar tudo e reindexar —, a busca ficaria vazia
    /// durante a reconstrução, e uma falha no meio deixaria o índice parcial.
    /// </remarks>
    /// <param name="ct">Token de cancelamento.</param>
    /// <response code="204">Índice reconstruído com sucesso.</response>
    /// <response code="401">Autenticação ausente ou inválida.</response>
    /// <response code="403">O usuário não é administrador.</response>
    /// <response code="503">OpenSearch não configurado ou fora do ar.</response>
    [HttpPost("reindex")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Reindexar(CancellationToken ct)
    {
        const int tamanhoLote = 500;
        await indiceDeJogos.GarantirIndiceAsync(ct);

        var idsAtivos = new List<string>();
        var pagina = 1;
        long total;
        do
        {
            var resultado = await jogoService.ListarAsync(pagina, tamanhoLote, null, ct);
            total = resultado.Total;
            await indiceDeJogos.IndexarLoteAsync(resultado.Itens, ct);
            idsAtivos.AddRange(resultado.Itens.Select(jogo => jogo.Id));
            pagina++;
        }
        while ((long)(pagina - 1) * tamanhoLote < total);

        await indiceDeJogos.RemoverExcetoAsync(idsAtivos, ct);
        return NoContent();
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
