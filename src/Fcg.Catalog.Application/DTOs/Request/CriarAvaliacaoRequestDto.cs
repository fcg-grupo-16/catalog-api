namespace Fcg.Catalog.Application.DTOs.Request;

/// <param name="JogoId">Jogo avaliado.</param>
/// <param name="Nota">Nota de 1 a 5.</param>
/// <param name="Comentario">Texto da avaliação.</param>
/// <param name="Titulo">Título curto, opcional.</param>
/// <param name="Tags">Tags livres, opcional (máx. 10).</param>
/// <param name="Contexto">
/// Metadados livres (ex.: <c>{"plataforma":"PC","horasJogadas":"42"}</c>). Campos que ainda não
/// existem podem ser enviados aqui sem alteração de schema — é o ponto do documento flexível.
/// </param>
public sealed record CriarAvaliacaoRequestDto(
    string JogoId,
    int Nota,
    string Comentario,
    string? Titulo = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyDictionary<string, string>? Contexto = null);