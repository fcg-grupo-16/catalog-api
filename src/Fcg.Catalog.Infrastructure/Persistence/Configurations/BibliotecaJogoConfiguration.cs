using Fcg.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MongoDB.Bson;
using MongoDB.EntityFrameworkCore.Extensions;

namespace Fcg.Catalog.Infrastructure.Persistence.Configurations;

public sealed class BibliotecaJogoConfiguration : IEntityTypeConfiguration<BibliotecaJogo>
{
    public void Configure(EntityTypeBuilder<BibliotecaJogo> builder)
    {
        builder.ToCollection("biblioteca_jogos");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasBsonRepresentation(BsonType.ObjectId)
            .HasValueGenerator<ObjectIdStringValueGenerator>();
        builder.Property(b => b.Id).Metadata.Sentinel = string.Empty;

        builder.Property(b => b.UsuarioId)
            .IsRequired();

        builder.Property(b => b.JogoId)
            .IsRequired();

        builder.Property(b => b.DataAquisicao)
            .IsRequired();

        builder.HasIndex(b => b.UsuarioId);

        builder.HasIndex(b => new { b.UsuarioId, b.JogoId })
            .IsUnique();
    }
}
