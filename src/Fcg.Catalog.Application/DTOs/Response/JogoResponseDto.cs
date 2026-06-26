using Fcg.Catalog.Domain.Enums;

namespace Fcg.Catalog.Application.DTOs.Response;

public sealed record JogoResponseDto(
    string Id,
    string Titulo,
    string Descricao,
    GeneroJogo Genero,
    decimal Preco,
    string Moeda,
    DateTime DataLancamento,
    bool Ativo);
