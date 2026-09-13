using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Exceptions;
using Fcg.Catalog.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Fcg.Catalog.Infrastructure.Repositories;

/// <summary>
/// Persistência de avaliações com o DRIVER NATIVO do MongoDB (<c>MongoDB.Driver</c>), não via
/// EF Core.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que driver nativo aqui, se o resto do serviço usa EF?</b> Esta collection precisa de
/// coisas que o provider EF Core do Mongo não expõe (ou expõe mal): aggregation pipeline com
/// <c>$facet</c>, atualização atômica com <c>$inc</c>, e sub-documento de chaves livres. Usar o
/// driver direto aqui é a decisão certa — e é o mesmo caminho já adotado em
/// <c>PedidoRepository</c>.
/// </para>
/// <para>
/// <b>Nada de transação.</b> Toda escrita aqui é de UM documento só, e escrita de documento único
/// é atômica no MongoDB por definição. Abrir sessão/transação seria custo puro.
/// </para>
/// </remarks>
public sealed class AvaliacaoRepository : IAvaliacaoRepository
{
    private const string CollectionName = "avaliacoes";

    private readonly IMongoCollection<AvaliacaoDocument> _avaliacoes;
    private readonly ILogger<AvaliacaoRepository> _logger;

    public AvaliacaoRepository(IMongoDatabase database, ILogger<AvaliacaoRepository> logger)
    {
        _avaliacoes = database.GetCollection<AvaliacaoDocument>(CollectionName);
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A regra "uma avaliação por usuário por jogo" é garantida pelo ÍNDICE UNIQUE, não por um
    /// "SELECT antes do INSERT". Checar-e-inserir tem janela de corrida (dois requests simultâneos
    /// passam os dois pelo check); o índice unique é a única garantia real. Aqui traduzimos a
    /// violação (código 11000) na exceção de domínio.
    /// </remarks>
    public async Task<Avaliacao> CriarAsync(Avaliacao avaliacao, CancellationToken ct = default)
    {
        var doc = AvaliacaoDocument.FromEntity(avaliacao, ObjectId.GenerateNewId().ToString());

        try
        {
            await _avaliacoes.InsertOneAsync(doc, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new ConflitoDeDadosException(
                $"O usuário já avaliou o jogo {avaliacao.JogoId}. Remova a avaliação anterior para avaliar de novo.");
        }

        return doc.ToEntity();
    }

    public async Task<IReadOnlyList<Avaliacao>> ListarPorJogoAsync(
        string jogoId, int pagina, int tamanhoPagina, CancellationToken ct = default)
    {
        // Ordem + paginação servidas pelo índice composto (JogoId asc, DataCriacao desc): o Mongo
        // percorre o índice na ordem pedida, sem SORT em memória. Ver GarantirIndicesAsync.
        var docs = await _avaliacoes
            .Find(d => d.JogoId == jogoId)
            .SortByDescending(d => d.DataCriacao)
            .Skip((pagina - 1) * tamanhoPagina)
            .Limit(tamanhoPagina)
            .ToListAsync(ct);

        return docs.Select(d => d.ToEntity()).ToList();
    }

    public Task<long> ContarPorJogoAsync(string jogoId, CancellationToken ct = default) =>
        _avaliacoes.CountDocumentsAsync(d => d.JogoId == jogoId, cancellationToken: ct);

    /// <inheritdoc />
    /// <remarks>
    /// <b>Aggregation pipeline com <c>$facet</c>.</b> Um único round-trip devolve os dois recortes:
    /// <list type="bullet">
    ///   <item><c>geral</c> — <c>$group</c> com <c>$avg</c> e <c>$sum</c> (média e total)</item>
    ///   <item><c>porNota</c> — <c>$group</c> por nota (o histograma 1..5)</item>
    /// </list>
    /// Em relacional seriam duas queries (ou uma com subselect); e recalcular isso a cada request é
    /// exatamente o tipo de "consulta onerosa" que o cache Redis da <c>catalog-api#21</c> resolve.
    /// <para>
    /// O <c>$match</c> vem PRIMEIRO no pipeline de propósito: assim ele usa o índice de
    /// <c>JogoId</c> e a agregação só toca os documentos daquele jogo, em vez de varrer a
    /// collection inteira.
    /// </para>
    /// </remarks>
    public async Task<AvaliacaoResumo> ObterResumoAsync(string jogoId, CancellationToken ct = default)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("JogoId", jogoId)),
            new BsonDocument("$facet", new BsonDocument
            {
                ["geral"] = new BsonArray
                {
                    new BsonDocument("$group", new BsonDocument
                    {
                        ["_id"] = BsonNull.Value,
                        ["total"] = new BsonDocument("$sum", 1),
                        ["media"] = new BsonDocument("$avg", "$Nota")
                    })
                },
                ["porNota"] = new BsonArray
                {
                    new BsonDocument("$group", new BsonDocument
                    {
                        ["_id"] = "$Nota",
                        ["quantidade"] = new BsonDocument("$sum", 1)
                    })
                }
            })
        };

        var resultado = await _avaliacoes
            .Aggregate<BsonDocument>(pipeline, cancellationToken: ct)
            .FirstOrDefaultAsync(ct);

        // Distribuição SEMPRE com as 5 chaves, inclusive as zeradas: o front desenha o histograma
        // sem ter de tratar buracos.
        var distribuicao = Enumerable.Range(Avaliacao.NotaMinima, Avaliacao.NotaMaxima)
            .ToDictionary(nota => nota, _ => 0L);

        long total = 0;
        decimal media = 0;

        // $facet sempre devolve UM documento; os arrays internos podem estar VAZIOS (jogo sem
        // nenhuma avaliação). Por isso todo acesso abaixo é defensivo.
        if (resultado is not null)
        {
            if (resultado.TryGetValue("geral", out var geral)
                && geral is BsonArray { Count: > 0 } geralArray
                && geralArray[0] is BsonDocument geralDoc)
            {
                total = geralDoc.GetValue("total", 0).ToInt64();
                media = Math.Round((decimal)geralDoc.GetValue("media", 0).ToDouble(), 2);
            }

            if (resultado.TryGetValue("porNota", out var porNota) && porNota is BsonArray porNotaArray)
            {
                foreach (var item in porNotaArray.OfType<BsonDocument>())
                {
                    var nota = item.GetValue("_id", 0).ToInt32();
                    // Guarda contra documento com nota fora de 1..5 gravado por versão anterior do
                    // código: ignora em vez de estourar KeyNotFoundException.
                    if (distribuicao.ContainsKey(nota))
                        distribuicao[nota] = item.GetValue("quantidade", 0).ToInt64();
                }
            }
        }

        return new AvaliacaoResumo(jogoId, total, media, distribuicao);
    }

    public async Task<Avaliacao?> ObterPorIdAsync(string id, CancellationToken ct = default)
    {
        // Id inválido (não é ObjectId) => trate como "não encontrado". Sem esta guarda o driver
        // lança FormatException na desserialização do _id e o cliente leva 500 em vez de 404.
        if (!ObjectId.TryParse(id, out _))
            return null;

        var doc = await _avaliacoes.Find(d => d.Id == id).FirstOrDefaultAsync(ct);
        return doc?.ToEntity();
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>$inc</c> com <c>FindOneAndUpdate</c>: incremento ATÔMICO no servidor, sem ler-somar-gravar
    /// na aplicação. Mil votos simultâneos resultam em mil — um read-modify-write perderia
    /// atualizações (lost update).
    /// </remarks>
    public async Task<int?> IncrementarVotoUtilAsync(string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _))
            return null;

        var atualizado = await _avaliacoes.FindOneAndUpdateAsync<AvaliacaoDocument>(
            Builders<AvaliacaoDocument>.Filter.Eq(d => d.Id, id),
            Builders<AvaliacaoDocument>.Update.Inc(d => d.VotosUteis, 1),
            new FindOneAndUpdateOptions<AvaliacaoDocument>
            {
                ReturnDocument = ReturnDocument.After   // devolve o valor JÁ incrementado
            },
            ct);

        return atualizado?.VotosUteis;
    }

    public async Task<bool> RemoverAsync(string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _))
            return false;

        var resultado = await _avaliacoes.DeleteOneAsync(d => d.Id == id, ct);
        return resultado.DeletedCount > 0;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>Os dois índices, e por que cada um existe:</b>
    /// <list type="number">
    ///   <item>
    ///     <c>(JogoId asc, DataCriacao desc)</c> — serve o feed paginado. Sem ele, listar as
    ///     avaliações de um jogo faria COLLECTION SCAN + sort em memória; com milhões de
    ///     documentos o Mongo aborta o sort (limite de 32 MB) e a rota passa a dar erro.
    ///   </item>
    ///   <item>
    ///     <c>(JogoId asc, UsuarioId asc) UNIQUE</c> — é a regra "uma avaliação por usuário por
    ///     jogo", imposta pelo BANCO. Validar isso só na aplicação deixa janela de corrida.
    ///   </item>
    /// </list>
    /// Idempotente: <c>CreateMany</c> com índice já existente e definição idêntica é no-op.
    /// </remarks>
    public async Task GarantirIndicesAsync(CancellationToken ct = default)
    {
        try
        {
            await _avaliacoes.Indexes.CreateManyAsync(
                [
                    new CreateIndexModel<AvaliacaoDocument>(
                        Builders<AvaliacaoDocument>.IndexKeys
                            .Ascending(d => d.JogoId)
                            .Descending(d => d.DataCriacao),
                        new CreateIndexOptions { Name = "ix_jogo_data" }),

                    new CreateIndexModel<AvaliacaoDocument>(
                        Builders<AvaliacaoDocument>.IndexKeys
                            .Ascending(d => d.JogoId)
                            .Ascending(d => d.UsuarioId),
                        new CreateIndexOptions { Name = "ux_jogo_usuario", Unique = true })
                ],
                ct);
        }
        catch (Exception ex)
        {
            // Best-effort, igual ao padrão do notifications-api: Mongo indisponível no boot não
            // pode impedir o pod de subir (o initContainer wait-for-mongodb já mitiga).
            // ⚠️ Consequência real: sem o índice unique, duplicatas passam a ser possíveis até a
            // próxima subida. Por isso o log é Warning e não Debug.
            _logger.LogWarning(ex, "Falha ao garantir os índices da collection {Collection}.", CollectionName);
        }
    }

    /// <summary>
    /// Representação em documento. Separada da entidade de domínio para os atributos de mapeamento
    /// (<c>[BsonId]</c>, <c>[BsonRepresentation]</c>) não vazarem para o Domain — mesmo padrão de
    /// <c>PedidoRepository</c>.
    /// </summary>
    private sealed class AvaliacaoDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; init; } = string.Empty;

        public string JogoId { get; init; } = string.Empty;
        public string UsuarioId { get; init; } = string.Empty;
        public int Nota { get; init; }

        // Campo OPCIONAL: com IgnoreIfNull, avaliações sem título simplesmente NÃO TÊM o campo no
        // documento — é a flexibilidade de schema do NoSQL, não um NULL numa coluna.
        [BsonIgnoreIfNull]
        public string? Titulo { get; init; }

        public string Comentario { get; init; } = string.Empty;
        public List<string> Tags { get; init; } = [];

        /// <summary>Sub-documento de chaves livres.</summary>
        public Dictionary<string, string> Contexto { get; init; } = [];

        public int VotosUteis { get; init; }
        public DateTime DataCriacao { get; init; }

        public static AvaliacaoDocument FromEntity(Avaliacao a, string id) => new()
        {
            Id = string.IsNullOrWhiteSpace(a.Id) ? id : a.Id,
            JogoId = a.JogoId,
            UsuarioId = a.UsuarioId,
            Nota = a.Nota,
            Titulo = a.Titulo,
            Comentario = a.Comentario,
            Tags = [.. a.Tags],
            Contexto = new Dictionary<string, string>(a.Contexto),
            VotosUteis = a.VotosUteis,
            DataCriacao = a.DataCriacao
        };

        public Avaliacao ToEntity() => Avaliacao.Restaurar(
            Id, JogoId, UsuarioId, Nota, Titulo, Comentario, Tags, Contexto, VotosUteis, DataCriacao);
    }
}