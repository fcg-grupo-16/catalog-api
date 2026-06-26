using Fcg.Catalog.Application.Interfaces;
using Fcg.Catalog.Application.Services;
using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Enums;
using Fcg.Catalog.Domain.Exceptions;
using Fcg.Catalog.Domain.Repositories;
using Fcg.Catalog.Domain.ValueObjects;
using Fcg.Contracts.Events;
using FluentAssertions;
using Moq;

namespace Fcg.Catalog.UnitTests.Services;

public class PurchaseServiceTests
{
    private readonly Mock<IJogoRepository> _jogoMock = new();
    private readonly Mock<IBibliotecaRepository> _bibliotecaMock = new();
    private readonly Mock<IEventPublisher> _publisherMock = new();
    private readonly PurchaseService _service;

    public PurchaseServiceTests()
    {
        _service = new PurchaseService(_jogoMock.Object, _bibliotecaMock.Object, _publisherMock.Object);
    }

    private static Jogo CriarJogoAtivo() =>
        new("Jogo Teste", "Descrição", GeneroJogo.RPG, new Preco(59.90m), DateTime.Now);

    [Fact]
    public async Task IniciarCompraAsync_DevePublicarOrderPlacedEvent_QuandoDadosValidos()
    {
        var jogo = CriarJogoAtivo();
        _jogoMock.Setup(r => r.ObterPorIdAsync("jogo-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(jogo);
        _bibliotecaMock.Setup(r => r.UsuarioPossuiJogoAsync("usuario-id", "jogo-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _service.IniciarCompraAsync("usuario-id", "jogo-id");

        _publisherMock.Verify(p => p.PublishAsync(
            It.Is<OrderPlacedEvent>(e =>
                e.UserId == "usuario-id" &&
                e.GameId == "jogo-id" &&
                e.Price == 59.90m &&
                e.OrderId != Guid.Empty),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IniciarCompraAsync_DeveLancarExcecao_QuandoJogoNaoEncontrado()
    {
        _jogoMock.Setup(r => r.ObterPorIdAsync("jogo-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Jogo?)null);

        var act = () => _service.IniciarCompraAsync("usuario-id", "jogo-id");

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<OrderPlacedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IniciarCompraAsync_DeveLancarExcecao_QuandoJogoInativo()
    {
        var jogo = CriarJogoAtivo();
        jogo.Desativar();
        _jogoMock.Setup(r => r.ObterPorIdAsync("jogo-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(jogo);

        var act = () => _service.IniciarCompraAsync("usuario-id", "jogo-id");

        await act.Should().ThrowAsync<ValidacaoException>()
            .WithMessage("*inativo*");
        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<OrderPlacedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IniciarCompraAsync_DeveLancarExcecao_QuandoJogoJaPossuido()
    {
        var jogo = CriarJogoAtivo();
        _jogoMock.Setup(r => r.ObterPorIdAsync("jogo-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(jogo);
        _bibliotecaMock.Setup(r => r.UsuarioPossuiJogoAsync("usuario-id", "jogo-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _service.IniciarCompraAsync("usuario-id", "jogo-id");

        await act.Should().ThrowAsync<ConflitoDeDadosException>();
        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<OrderPlacedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
