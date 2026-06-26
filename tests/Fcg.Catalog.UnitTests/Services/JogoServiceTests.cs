using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.Services;
using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Enums;
using Fcg.Catalog.Domain.Exceptions;
using Fcg.Catalog.Domain.Repositories;
using Fcg.Catalog.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Fcg.Catalog.UnitTests.Services;

public class JogoServiceTests
{
    private readonly Mock<IJogoRepository> _repositoryMock = new();
    private readonly JogoService _service;

    public JogoServiceTests()
    {
        _service = new JogoService(_repositoryMock.Object);
    }

    [Fact]
    public async Task CriarAsync_DeveRetornarJogo_QuandoDadosValidos()
    {
        var dto = new CriarJogoRequestDto("Novo Jogo", "Descrição", GeneroJogo.RPG, 59.90m, DateTime.Now);
        _repositoryMock.Setup(r => r.TituloExisteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CriarAsync(dto);

        result.Titulo.Should().Be("Novo Jogo");
        result.Genero.Should().Be(GeneroJogo.RPG);
        result.Preco.Should().Be(59.90m);
        _repositoryMock.Verify(r => r.CriarAsync(It.IsAny<Jogo>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CriarAsync_DeveLancarExcecao_QuandoTituloJaExiste()
    {
        var dto = new CriarJogoRequestDto("Existente", "Desc", GeneroJogo.Acao, 10, DateTime.Now);
        _repositoryMock.Setup(r => r.TituloExisteAsync("Existente", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _service.CriarAsync(dto);

        await act.Should().ThrowAsync<ConflitoDeDadosException>();
    }

    [Fact]
    public async Task ObterPorIdAsync_DeveLancarExcecao_QuandoNaoEncontrado()
    {
        _repositoryMock.Setup(r => r.ObterPorIdAsync("id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Jogo?)null);

        var act = () => _service.ObterPorIdAsync("id");

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task RemoverAsync_DeveDesativarJogo()
    {
        var jogo = new Jogo("Título", "Desc", GeneroJogo.Acao, new Preco(10), DateTime.Now);
        _repositoryMock.Setup(r => r.ObterPorIdAsync("id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(jogo);

        await _service.RemoverAsync("id");

        jogo.Ativo.Should().BeFalse();
        _repositoryMock.Verify(r => r.AtualizarAsync(jogo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterPorIdAsync_DeveRetornarJogo_QuandoEncontrado()
    {
        var jogo = new Jogo("Título", "Desc", GeneroJogo.RPG, new Preco(49.90m), DateTime.Now);
        _repositoryMock.Setup(r => r.ObterPorIdAsync("id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(jogo);

        var result = await _service.ObterPorIdAsync("id");

        result.Titulo.Should().Be("Título");
        result.Genero.Should().Be(GeneroJogo.RPG);
        result.Preco.Should().Be(49.90m);
    }

    [Fact]
    public async Task ListarAsync_DeveRetornarTodosOsJogos()
    {
        var jogos = new List<Jogo>
        {
            new("Jogo 1", "Desc 1", GeneroJogo.Acao, new Preco(10), DateTime.Now),
            new("Jogo 2", "Desc 2", GeneroJogo.RPG, new Preco(20), DateTime.Now)
        };
        _repositoryMock.Setup(r => r.ObterTodosAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jogos);

        var result = await _service.ListarAsync(1, 10, null);

        result.Itens.Should().HaveCount(2);
        result.Pagina.Should().Be(1);
    }

    [Fact]
    public async Task ListarAsync_DeveRetornarJogosFiltradosPorGenero()
    {
        var jogos = new List<Jogo>
        {
            new("RPG Game", "Desc", GeneroJogo.RPG, new Preco(30), DateTime.Now)
        };
        _repositoryMock.Setup(r => r.BuscarPorGeneroAsync(GeneroJogo.RPG, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jogos);

        var result = await _service.ListarAsync(1, 10, GeneroJogo.RPG);

        result.Itens.Should().HaveCount(1);
        result.Itens.First().Genero.Should().Be(GeneroJogo.RPG);
    }

    [Fact]
    public async Task AtualizarAsync_DeveRetornarJogoAtualizado_QuandoDadosValidos()
    {
        var jogo = new Jogo("Título", "Desc", GeneroJogo.Acao, new Preco(10), DateTime.Now);
        _repositoryMock.Setup(r => r.ObterPorIdAsync("id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(jogo);
        _repositoryMock.Setup(r => r.TituloExisteAsync("Novo Título", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var dto = new AtualizarJogoRequestDto("Novo Título", "Nova Desc", GeneroJogo.RPG, 29.90m);
        var result = await _service.AtualizarAsync("id", dto);

        result.Titulo.Should().Be("Novo Título");
        result.Descricao.Should().Be("Nova Desc");
        result.Genero.Should().Be(GeneroJogo.RPG);
        result.Preco.Should().Be(29.90m);
    }

    [Fact]
    public async Task AtualizarAsync_DeveLancarExcecao_QuandoTituloDuplicado()
    {
        var jogo = new Jogo("Título", "Desc", GeneroJogo.Acao, new Preco(10), DateTime.Now);
        _repositoryMock.Setup(r => r.ObterPorIdAsync("id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(jogo);
        _repositoryMock.Setup(r => r.TituloExisteAsync("Existente", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var dto = new AtualizarJogoRequestDto("Existente", "Desc", GeneroJogo.Acao, 10);
        var act = () => _service.AtualizarAsync("id", dto);

        await act.Should().ThrowAsync<ConflitoDeDadosException>();
    }

    [Fact]
    public async Task AtualizarAsync_DeveLancarExcecao_QuandoJogoNaoEncontrado()
    {
        _repositoryMock.Setup(r => r.ObterPorIdAsync("id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Jogo?)null);

        var dto = new AtualizarJogoRequestDto("Título", "Desc", GeneroJogo.Acao, 10);
        var act = () => _service.AtualizarAsync("id", dto);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task RemoverAsync_DeveLancarExcecao_QuandoJogoNaoEncontrado()
    {
        _repositoryMock.Setup(r => r.ObterPorIdAsync("id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Jogo?)null);

        var act = () => _service.RemoverAsync("id");

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }
}
