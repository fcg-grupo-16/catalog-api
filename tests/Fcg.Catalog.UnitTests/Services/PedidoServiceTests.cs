using Fcg.Catalog.Application.Services;
using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Exceptions;
using Fcg.Catalog.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace Fcg.Catalog.UnitTests.Services;

public class PedidoServiceTests
{
    private readonly Mock<IPedidoRepository> _repositoryMock = new();
    private readonly PedidoService _service;

    public PedidoServiceTests()
    {
        _service = new PedidoService(_repositoryMock.Object);
    }

    [Fact]
    public async Task ObterPorOrderIdAsync_DeveRetornarPedido_QuandoEncontrado()
    {
        var pedido = new Pedido("order-1", "user-1", "game-1", 59.90m);
        _repositoryMock.Setup(r => r.ObterPorOrderIdAsync("order-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pedido);

        var result = await _service.ObterPorOrderIdAsync("order-1");

        result.OrderId.Should().Be("order-1");
        result.UserId.Should().Be("user-1");
        result.GameId.Should().Be("game-1");
        result.Preco.Should().Be(59.90m);
        result.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task ObterPorOrderIdAsync_DeveLancarNaoEncontrado_QuandoInexistente()
    {
        _repositoryMock.Setup(r => r.ObterPorOrderIdAsync("inexistente", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pedido?)null);

        var act = () => _service.ObterPorOrderIdAsync("inexistente");

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }
}
