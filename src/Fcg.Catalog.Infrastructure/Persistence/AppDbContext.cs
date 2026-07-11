using Fcg.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using MongoDB.Bson;

namespace Fcg.Catalog.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Jogo> Jogos => Set<Jogo>();
    public DbSet<BibliotecaJogo> BibliotecaJogos => Set<BibliotecaJogo>();

    // Pedido NÃO é mapeado via EF: é persistido pelo driver nativo dentro da transação do
    // MongoDbContext do MassTransit (outbox transacional) — ver PedidoRepository.

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

public sealed class ObjectIdStringValueGenerator : ValueGenerator<string>
{
    public override string Next(EntityEntry entry) => ObjectId.GenerateNewId().ToString();
    public override bool GeneratesTemporaryValues => false;
}
