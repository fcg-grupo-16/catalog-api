namespace Fcg.Catalog.Application.DTOs.Response;

public sealed record AvaliacaoResumoResponseDto(
    string JogoId,
    long Total,
    decimal MediaNota,
    IReadOnlyDictionary<int, long> Distribuicao);
