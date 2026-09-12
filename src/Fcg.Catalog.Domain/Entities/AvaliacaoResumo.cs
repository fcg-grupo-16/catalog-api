namespace Fcg.Catalog.Domain.Entities;

/// <summary>
/// Resultado da AGREGAÇÃO de avaliações de um jogo: média, total e distribuição de notas.
/// </summary>
/// <remarks>
/// Não é uma entidade persistida — é o retorno de um aggregation pipeline. Em relacional isso
/// seriam duas queries (`AVG`+`COUNT` e um `GROUP BY nota`); com <c>$facet</c> o Mongo devolve as
/// duas num único round-trip.
/// </remarks>
/// <param name="JogoId">Jogo avaliado.</param>
/// <param name="Total">Quantidade de avaliações.</param>
/// <param name="MediaNota">Média das notas, arredondada em 2 casas. 0 quando não há avaliações.</param>
/// <param name="Distribuicao">Quantas avaliações por nota (chaves 1..5, sempre todas presentes).</param>
public sealed record AvaliacaoResumo(
    string JogoId,
    long Total,
    decimal MediaNota,
    IReadOnlyDictionary<int, long> Distribuicao);