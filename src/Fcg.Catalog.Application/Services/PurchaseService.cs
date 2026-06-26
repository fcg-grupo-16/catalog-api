using Fcg.Catalog.Application.Interfaces;
using Fcg.Catalog.Domain.Exceptions;
using Fcg.Catalog.Domain.Repositories;
using Fcg.Contracts.Events;

namespace Fcg.Catalog.Application.Services;

public sealed class PurchaseService(
    IJogoRepository jogoRepository,
    IBibliotecaRepository bibliotecaRepository,
    IEventPublisher eventPublisher) : IPurchaseService
{
    public async Task IniciarCompraAsync(string usuarioId, string jogoId, CancellationToken ct = default)
    {
        var jogo = await jogoRepository.ObterPorIdAsync(jogoId, ct)
            ?? throw new EntidadeNaoEncontradaException("Jogo", jogoId);

        if (!jogo.Ativo)
        {
            throw new ValidacaoException("O jogo está inativo e não pode ser adquirido.");
        }

        if (await bibliotecaRepository.UsuarioPossuiJogoAsync(usuarioId, jogoId, ct))
        {
            throw new ConflitoDeDadosException($"O usuário já possui o jogo '{jogo.Titulo}' em sua biblioteca.");
        }

        var orderId = Guid.NewGuid();

        await eventPublisher.PublishAsync(
            new OrderPlacedEvent
            {
                OrderId = orderId,
                UserId = usuarioId,
                GameId = jogoId,
                Price = jogo.Preco.Valor
            },
            ct);
    }
}
