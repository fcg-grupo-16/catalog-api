using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Enums;
using Fcg.Catalog.Domain.Repositories;
using Fcg.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fcg.Catalog.Infrastructure.Repositories;

public sealed class JogoRepository(AppDbContext context) : IJogoRepository
{
    public async Task<Jogo?> ObterPorIdAsync(string id, CancellationToken ct = default) =>
        await context.Jogos.FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task<IEnumerable<Jogo>> ObterTodosAsync(int pagina, int tamanhoPagina, CancellationToken ct = default) =>
        await context.Jogos
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);

    public async Task<IEnumerable<Jogo>> BuscarPorGeneroAsync(GeneroJogo genero, int pagina, int tamanhoPagina, CancellationToken ct = default) =>
        await context.Jogos
            .Where(j => j.Genero == genero)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);

    public async Task CriarAsync(Jogo jogo, CancellationToken ct = default)
    {
        context.Jogos.Add(jogo);
        await context.SaveChangesAsync(ct);
    }

    public async Task CriarLote(IEnumerable<Jogo> jogos, CancellationToken ct = default)
    {
        context.Jogos.AddRange(jogos);
        await context.SaveChangesAsync(ct);
    }

    public async Task AtualizarAsync(Jogo jogo, CancellationToken ct = default)
    {
        context.Jogos.Update(jogo);
        await context.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(string id, CancellationToken ct = default)
    {
        var jogo = await context.Jogos.FirstOrDefaultAsync(j => j.Id == id, ct);
        if (jogo is not null)
        {
            context.Jogos.Remove(jogo);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> TituloExisteAsync(string titulo, CancellationToken ct = default) =>
        await context.Jogos.AnyAsync(j => j.Titulo == titulo, ct);
}
