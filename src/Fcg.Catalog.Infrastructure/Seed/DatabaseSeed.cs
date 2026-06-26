using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Enums;
using Fcg.Catalog.Domain.ValueObjects;
using Fcg.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fcg.Catalog.Infrastructure.Seed;

public static class DatabaseSeed
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        await SeedJogosAsync(context, logger, ct);
    }

    private static async Task SeedJogosAsync(
        AppDbContext context,
        ILogger logger,
        CancellationToken ct)
    {
        var count = await context.Jogos.CountAsync(ct);

        if (count > 0)
        {
            logger.LogInformation("Seed: Jogos já existem ({Count} encontrados). Pulando...", count);
            return;
        }

        var jogos = new[]
        {
            new Jogo("CodeQuest: A Jornada do Desenvolvedor", "Um RPG educacional onde você aprende programação enquanto evolui seu personagem.", GeneroJogo.RPG, new Preco(49.90m), new DateTime(2024, 3, 15)),
            new Jogo("Bug Hunter: Caçador de Defeitos", "Resolva puzzles baseados em bugs reais de software.", GeneroJogo.Puzzle, new Preco(29.90m), new DateTime(2024, 5, 20)),
            new Jogo("Cloud Arena: Batalha dos Servidores", "Gerencie infraestrutura cloud em tempo real e vença seus oponentes.", GeneroJogo.Estrategia, new Preco(59.90m), new DateTime(2024, 7, 10)),
            new Jogo("API Rush: Corrida das Requisições", "Uma corrida frenética onde suas requisições competem pela melhor performance.", GeneroJogo.Corrida, new Preco(39.90m), new DateTime(2024, 9, 1)),
            new Jogo("Cyber Shield: Defesa Digital", "Defenda sua rede contra ataques cibernéticos em tempo real.", GeneroJogo.Acao, new Preco(44.90m), new DateTime(2024, 11, 25))
        };

        context.Jogos.AddRange(jogos);
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Seed: {Count} jogos de exemplo criados com sucesso", jogos.Length);
    }
}
