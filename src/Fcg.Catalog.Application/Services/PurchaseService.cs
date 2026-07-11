using Fcg.Catalog.Application.Interfaces;
using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Exceptions;
using Fcg.Catalog.Domain.Repositories;
using Fcg.Contracts.Events;

namespace Fcg.Catalog.Application.Services;

public sealed class PurchaseService(
    IJogoRepository jogoRepository,
    IBibliotecaRepository bibliotecaRepository,
    IPedidoRepository pedidoRepository,
    IEventPublisher eventPublisher) : IPurchaseService
{
    public async Task<string> IniciarCompraAsync(string usuarioId, string jogoId, CancellationToken ct = default)
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

        // Outbox transacional: o Pedido e a mensagem OrderPlacedEvent são gravados no MESMO
        // SaveChanges (SalvarAlteracoesAsync) — ou ambos persistem, ou nenhum. Elimina o
        // dual-write (persistir o pedido e publicar o evento em transações separadas).
        var pedido = new Pedido(orderId.ToString(), usuarioId, jogoId, jogo.Preco.Valor);
        await pedidoRepository.AdicionarSemSalvarAsync(pedido, ct);

        await eventPublisher.PublishAsync(
            new OrderPlacedEvent
            {
                OrderId = orderId,
                UserId = usuarioId,
                GameId = jogoId,
                Price = jogo.Preco.Valor
            },
            ct);

        await pedidoRepository.SalvarAlteracoesAsync(ct);

        return orderId.ToString();
    }
}
