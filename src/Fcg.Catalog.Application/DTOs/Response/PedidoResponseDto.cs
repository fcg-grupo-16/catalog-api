
using Fcg.Catalog.Domain.Enums;

namespace Fcg.Catalog.Application.DTOs.Response;

public sealed record PedidoResponseDto(string OrderId,
    string UserId,
    string GameId,
    decimal Preco,
    string Status,
    DateTime DataCriacao,
    DateTime dataAtuaizacao);
