using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Enums;
using Fcg.Catalog.Domain.Repositories;
using MassTransit.MongoDbIntegration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Fcg.Catalog.Infrastructure.Repositories;

/// <summary>
/// Persistência do Pedido via driver nativo do MongoDB dentro da transação do
/// <see cref="MongoDbContext"/> do MassTransit. Isso permite gravar o Pedido e as mensagens
/// do outbox (OrderPlacedEvent) atomicamente — o outbox transacional exigido pela issue #3.
/// (O EF Core não participa da transação do MongoDbContext, por isso o Pedido não usa EF.)
/// </summary>
public sealed class PedidoRepository : IPedidoRepository
{
    private static int _indexEnsured;

    private readonly MongoDbContext _mongoDbContext;
    private readonly IMongoCollection<PedidoDocument> _pedidos;

    public PedidoRepository(IMongoDatabase mongoDatabase, MongoDbContext mongoDbContext)
    {
        _mongoDbContext = mongoDbContext;
        _pedidos = mongoDatabase.GetCollection<PedidoDocument>("pedidos");

        // Índice único em OrderId (chave de correlação). Criado uma vez por processo.
        if (Interlocked.Exchange(ref _indexEnsured, 1) == 0)
        {
            _pedidos.Indexes.CreateOne(new CreateIndexModel<PedidoDocument>(
                Builders<PedidoDocument>.IndexKeys.Ascending(d => d.OrderId),
                new CreateIndexOptions { Unique = true }));
        }
    }

    public async Task AdicionarSemSalvarAsync(Pedido pedido, CancellationToken ct = default)
    {
        var id = string.IsNullOrWhiteSpace(pedido.Id) ? ObjectId.GenerateNewId().ToString() : pedido.Id;

        await _mongoDbContext.BeginTransaction(ct);
        await _pedidos.InsertOneAsync(
            _mongoDbContext.Session,
            PedidoDocument.FromEntity(pedido, id),
            cancellationToken: ct);
    }

    public async Task SalvarAlteracoesAsync(CancellationToken ct = default)
    {
        if (_mongoDbContext.Session is not null)
        {
            await _mongoDbContext.CommitTransaction(ct);
        }
    }

    public async Task<Pedido?> ObterPorOrderIdAsync(string orderId, CancellationToken ct = default)
    {
        var doc = await _pedidos.Find(d => d.OrderId == orderId).FirstOrDefaultAsync(ct);
        return doc?.ToEntity();
    }

    public async Task AtualizarAsync(Pedido pedido, CancellationToken ct = default)
    {
        await _pedidos.ReplaceOneAsync(
            d => d.OrderId == pedido.OrderId,
            PedidoDocument.FromEntity(pedido, pedido.Id),
            cancellationToken: ct);
    }

    private sealed class PedidoDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; init; } = string.Empty;

        public string OrderId { get; init; } = string.Empty;
        public string UserId { get; init; } = string.Empty;
        public string GameId { get; init; } = string.Empty;

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Price { get; init; }

        public int Status { get; init; }
        public DateTime DataCriacao { get; init; }
        public DateTime DataAtualizacao { get; init; }

        public static PedidoDocument FromEntity(Pedido p, string id) => new()
        {
            Id = id,
            OrderId = p.OrderId,
            UserId = p.UserId,
            GameId = p.GameId,
            Price = p.Price,
            Status = (int)p.Status,
            DataCriacao = p.DataCriacao,
            DataAtualizacao = p.DataAtualizacao
        };

        public Pedido ToEntity() =>
            Pedido.Restaurar(Id, OrderId, UserId, GameId, Price, (StatusPedido)Status, DataCriacao, DataAtualizacao);
    }
}
