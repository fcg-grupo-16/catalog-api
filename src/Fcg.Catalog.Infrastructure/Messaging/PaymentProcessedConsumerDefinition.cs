using MassTransit;

namespace Fcg.Catalog.Infrastructure.Messaging;

/// <summary>
/// Habilita o INBOX transacional (Mongo) no endpoint do <see cref="PaymentProcessedConsumer"/>.
/// O <c>UseMongoDbOutbox</c> no receive endpoint faz deduplicação de redeliveries via
/// <c>InboxState</c> (efetivamente-once dentro da janela de detecção), complementando a
/// idempotência de negócio já existente no consumer — não é uma garantia absoluta de
/// exactly-once distribuído. Exige MongoDB replica set (transações multi-documento).
///
/// O retry fica no nível do bus (UseMessageRetry) — ou seja, POR FORA do outbox — de modo que
/// o inbox só marca a mensagem como processada após o sucesso, e retries transitórios não
/// geram duplicidade.
/// </summary>
public sealed class PaymentProcessedConsumerDefinition : ConsumerDefinition<PaymentProcessedConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<PaymentProcessedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMongoDbOutbox(context);
    }
}
