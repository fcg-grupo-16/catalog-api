namespace Fcg.Catalog.Application.DTOs.Response;

public sealed record AvaliacaoResponseDto(
    string Id,
    string JogoId,
    string UsuarioId,
    int Nota,
    string? Titulo,
    string Comentario,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, string> Contexto,
    int VotosUteis,
    DateTime DataCriacao);
