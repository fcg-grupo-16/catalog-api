using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Enums;
using Fcg.Catalog.Domain.Repositories;
using Fcg.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fcg.Catalog.Infrastructure.Messaging;

public sealed class PaymentProcessedConsumer(
    IPedidoRepository pedidoRepository,
    IBibliotecaRepository bibliotecaRepository,
    ILogger<PaymentProcessedConsumer> logger) : IConsumer<PaymentProcessedEvent>
{
    private const string StatusAprovado = "Approved";

    public async Task Consume(ConsumeContext<PaymentProcessedEvent> context)
    {
        var evt = context.Message;
        var ct = context.CancellationToken;
        var aprovado = string.Equals(evt.Status, StatusAprovado, StringComparison.OrdinalIgnoreCase);

        // Atualiza o status do pedido (idempotente: só transiciona a partir de Pending,
        // evitando reprocessar em caso de redelivery).
        var pedido = await pedidoRepository.ObterPorOrderIdAsync(evt.OrderId.ToString(), ct);
        if (pedido is null)
        {
            logger.LogWarning(
                "PaymentProcessedEvent recebido para o OrderId {OrderId} sem Pedido correspondente na base.",
                evt.OrderId);
        }
        else if (pedido.Status == StatusPedido.Pending)
        {
            if (aprovado)
            {
                pedido.Aprovar();
            }
            else
            {
                pedido.Rejeitar();
            }

            await pedidoRepository.AtualizarAsync(pedido, ct);
        }

        if (!aprovado)
        {
            logger.LogInformation(
                "Pagamento rejeitado para o pedido {OrderId} (usuário {UserId}, jogo {GameId}). Nenhuma ação na biblioteca.",
                evt.OrderId, evt.UserId, evt.GameId);
            return;
        }

        if (await bibliotecaRepository.UsuarioPossuiJogoAsync(evt.UserId, evt.GameId, ct))
        {
            logger.LogInformation(
                "Pagamento aprovado para o pedido {OrderId}, mas o usuário {UserId} já possui o jogo {GameId}. Ignorando (idempotência).",
                evt.OrderId, evt.UserId, evt.GameId);
            return;
        }

        var bibliotecaJogo = new BibliotecaJogo(evt.UserId, evt.GameId);
        await bibliotecaRepository.AdicionarJogoAsync(bibliotecaJogo, ct);

        logger.LogInformation(
            "Pagamento aprovado para o pedido {OrderId}. Jogo {GameId} adicionado à biblioteca do usuário {UserId}.",
            evt.OrderId, evt.GameId, evt.UserId);
    }
}
