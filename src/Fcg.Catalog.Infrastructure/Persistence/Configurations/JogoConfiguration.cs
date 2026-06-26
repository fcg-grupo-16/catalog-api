using Fcg.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MongoDB.Bson;
using MongoDB.EntityFrameworkCore.Extensions;

namespace Fcg.Catalog.Infrastructure.Persistence.Configurations;

public sealed class JogoConfiguration : IEntityTypeConfiguration<Jogo>
{
    public void Configure(EntityTypeBuilder<Jogo> builder)
    {
        builder.ToCollection("jogos");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id)
            .HasBsonRepresentation(BsonType.ObjectId)
            .HasValueGenerator<ObjectIdStringValueGenerator>();
        builder.Property(j => j.Id).Metadata.Sentinel = string.Empty;

        builder.Property(j => j.Titulo)
            .IsRequired();

        builder.Property(j => j.Descricao)
            .IsRequired();

        builder.Property(j => j.Genero)
            .IsRequired();

        builder.Property(j => j.DataLancamento)
            .IsRequired();

        builder.Property(j => j.DataCriacao)
            .IsRequired();

        builder.Property(j => j.Ativo)
            .IsRequired();

        builder.OwnsOne(j => j.Preco, preco =>
        {
            preco.Property(p => p.Valor)
                .HasElementName("valor")
                .IsRequired();

            preco.Property(p => p.Moeda)
                .HasElementName("moeda")
                .IsRequired();
        });

        builder.HasQueryFilter("SoftDelete", j => j.Ativo);

        builder.HasIndex(j => j.Titulo)
            .IsUnique();

        builder.HasIndex(j => j.Genero);
    }
}
