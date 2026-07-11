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

        var pedido = await pedidoRepository.ObterPorOrderIdAsync(evt.OrderId.ToString(), ct);
        if (pedido is null)
        {
            // O outbox garante que o Pedido é gravado antes de o OrderPlacedEvent ser publicado,
            // então a ausência aqui é uma anomalia de correlação: lança para retry/dead-letter em
            // vez de mascarar o problema (e evita side-effects sem pedido correspondente).
            throw new InvalidOperationException(
                $"PaymentProcessedEvent para OrderId {evt.OrderId} sem Pedido correspondente na base.");
        }

        // Transição de status idempotente: só a partir de Pending (evita reprocessar em redelivery).
        if (pedido.Status == StatusPedido.Pending)
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

        // Side-effects na biblioteca só quando o pedido está EFETIVAMENTE aprovado — evita
        // inconsistência caso chegue um evento Approved para um pedido já Rejected.
        if (pedido.Status != StatusPedido.Approved)
        {
            logger.LogInformation(
                "Pedido {OrderId} não aprovado (status {Status}). Nenhuma ação na biblioteca.",
                evt.OrderId, pedido.Status);
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
