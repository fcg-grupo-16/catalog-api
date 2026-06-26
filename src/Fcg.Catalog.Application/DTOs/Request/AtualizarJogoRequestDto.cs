using Fcg.Catalog.Domain.Enums;

namespace Fcg.Catalog.Application.DTOs.Request;

public sealed record AtualizarJogoRequestDto(
    string Titulo,
    string Descricao,
    GeneroJogo Genero,
    decimal Preco);
