using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Enums;
using Fcg.Catalog.Domain.Repositories;
using Fcg.Catalog.Infrastructure.Messaging;
using Fcg.Contracts.Events;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Fcg.Catalog.UnitTests.Messaging;

public class PaymentProcessedConsumerTests
{
    private readonly Mock<IPedidoRepository> _pedidoMock = new();
    private readonly Mock<IBibliotecaRepository> _bibliotecaMock = new();
    private readonly PaymentProcessedConsumer _consumer;

    public PaymentProcessedConsumerTests()
    {
        _consumer = new PaymentProcessedConsumer(
            _pedidoMock.Object,
            _bibliotecaMock.Object,
            NullLogger<PaymentProcessedConsumer>.Instance);
    }

    private static ConsumeContext<PaymentProcessedEvent> Ctx(PaymentProcessedEvent evt)
    {
        var mock = new Mock<ConsumeContext<PaymentProcessedEvent>>();
        mock.SetupGet(c => c.Message).Returns(evt);
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }

    [Fact]
    public async Task Consume_DeveAprovarPedidoEAdicionarNaBiblioteca_QuandoAprovado()
    {
        var orderId = Guid.NewGuid();
        var pedido = new Pedido(orderId.ToString(), "user-1", "game-1", 10m);
        _pedidoMock.Setup(r => r.ObterPorOrderIdAsync(orderId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pedido);
        _bibliotecaMock.Setup(r => r.UsuarioPossuiJogoAsync("user-1", "game-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var evt = new PaymentProcessedEvent { OrderId = orderId, UserId = "user-1", GameId = "game-1", Price = 10m, Status = "Approved" };
        await _consumer.Consume(Ctx(evt));

        pedido.Status.Should().Be(StatusPedido.Approved);
        _pedidoMock.Verify(r => r.AtualizarAsync(pedido, It.IsAny<CancellationToken>()), Times.Once);
        _bibliotecaMock.Verify(r => r.AdicionarJogoAsync(It.IsAny<BibliotecaJogo>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_DeveRejeitarPedidoENaoTocarBiblioteca_QuandoRejeitado()
    {
        var orderId = Guid.NewGuid();
        var pedido = new Pedido(orderId.ToString(), "user-1", "game-1", 10m);
        _pedidoMock.Setup(r => r.ObterPorOrderIdAsync(orderId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pedido);

        var evt = new PaymentProcessedEvent { OrderId = orderId, UserId = "user-1", GameId = "game-1", Price = 10m, Status = "Rejected" };
        await _consumer.Consume(Ctx(evt));

        pedido.Status.Should().Be(StatusPedido.Rejected);
        _pedidoMock.Verify(r => r.AtualizarAsync(pedido, It.IsAny<CancellationToken>()), Times.Once);
        _bibliotecaMock.Verify(r => r.AdicionarJogoAsync(It.IsAny<BibliotecaJogo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_DeveSerIdempotente_QuandoPedidoNaoEstaPending()
    {
        var orderId = Guid.NewGuid();
        var pedido = new Pedido(orderId.ToString(), "user-1", "game-1", 10m);
        pedido.Aprovar(); // já processado anteriormente

        _pedidoMock.Setup(r => r.ObterPorOrderIdAsync(orderId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pedido);
        _bibliotecaMock.Setup(r => r.UsuarioPossuiJogoAsync("user-1", "game-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var evt = new PaymentProcessedEvent { OrderId = orderId, UserId = "user-1", GameId = "game-1", Price = 10m, Status = "Approved" };
        await _consumer.Consume(Ctx(evt));

        _pedidoMock.Verify(r => r.AtualizarAsync(It.IsAny<Pedido>(), It.IsAny<CancellationToken>()), Times.Never);
        _bibliotecaMock.Verify(r => r.AdicionarJogoAsync(It.IsAny<BibliotecaJogo>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
