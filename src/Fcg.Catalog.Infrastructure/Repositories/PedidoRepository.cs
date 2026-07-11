using System;
using System.Collections.Generic;
using System.Text;
using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Repositories;
using Fcg.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fcg.Catalog.Infrastructure.Repositories;

public class PedidoRepository(AppDbContext context) : IPedidoRepository
{
    public async Task AtualizarAsync(Pedido pedido, CancellationToken ct = default)
    {
        context.Pedidos.Update(pedido);
        await context.SaveChangesAsync(ct);
    }
    public async Task CriarAsync(Pedido pedido, CancellationToken ct = default)
    {
        context.Pedidos.Add(pedido);
        await context.SaveChangesAsync(ct);
    }
    public async Task<Pedido?> ObterPorOrderIdAsync(string orderId, CancellationToken ct = default)
    {
        return await context.Pedidos.FirstOrDefaultAsync(p => p.OrderId == orderId);
    }
}
