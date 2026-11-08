namespace Fcg.Catalog.Application.DTOs.Response;

/// <summary>Representa um jogo encontrado pela busca, incluindo sua relevância.</summary>
public sealed record JogoBuscaResponseDto(
    string Id,
    string Titulo,
    string Descricao,
    decimal Preco,
    double Score);
