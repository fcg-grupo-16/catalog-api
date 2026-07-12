using MassTransit;

namespace Fcg.Catalog.Infrastructure.Messaging;

/// <summary>
/// Habilita o INBOX transacional (Mongo) no endpoint do <see cref="PaymentProcessedConsumer"/>.
/// O <c>UseMongoDbOutbox</c> no receive endpoint dá deduplicação de mensagens (garantia
/// exactly-once no consumo, via <c>InboxState</c>), complementando a idempotência de negócio
/// já existente no consumer. Exige MongoDB replica set (transações multi-documento).
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
