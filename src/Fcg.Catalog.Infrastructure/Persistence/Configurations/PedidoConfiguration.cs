using System;
using System.Collections.Generic;
using System.Text;
using Fcg.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MongoDB.Bson;
using MongoDB.EntityFrameworkCore.Extensions;

namespace Fcg.Catalog.Infrastructure.Persistence.Configurations;

public sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToCollection<Pedido>("pedidos");

        builder.HasKey(x => x.Id);

        builder.Property(p => p.Id)
                  .HasBsonRepresentation(BsonType.ObjectId)
                  .HasValueGenerator<ObjectIdStringValueGenerator>()
                  .HasSentinel(string.Empty);


        builder.Property(x => x.OrderId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.GameId)
            .IsRequired();

        builder.Property(x => x.Price)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.DataCriacao)
            .IsRequired();

        builder.Property(x => x.DataAtualizacao)
            .IsRequired();

    }
}
