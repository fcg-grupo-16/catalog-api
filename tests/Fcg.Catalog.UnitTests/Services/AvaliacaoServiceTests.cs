using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.Services;
using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Exceptions;
using Fcg.Catalog.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace Fcg.Catalog.UnitTests.Services;

public class AvaliacaoServiceTests
{
    private readonly Mock<IAvaliacaoRepository> _avaliacaoRepository = new();
    private readonly Mock<IJogoRepository> _jogoRepository = new();
    private readonly AvaliacaoService _service;

    public AvaliacaoServiceTests()
    {
        _service = new AvaliacaoService(_avaliacaoRepository.Object, _jogoRepository.Object);
    }

    [Fact]
    public async Task CriarAsync_DeveLancarEntidadeNaoEncontrada_QuandoJogoNaoExiste()
    {
        var dto = new CriarAvaliacaoRequestDto("jogo-1", 5, "comentário");
        _jogoRepository.Setup(r => r.ObterPorIdAsync(dto.JogoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Jogo?)null);

        var act = () => _service.CriarAsync("usuario-1", dto);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task RemoverAsync_DeveLancarAcessoNegado_QuandoAvaliacaoPertenceAOutroUsuario()
    {
        var avaliacao = new Avaliacao("jogo-1", "usuario-2", 5, "comentário");
        avaliacao = Avaliacao.Restaurar(
            "id-1",
            avaliacao.JogoId,
            avaliacao.UsuarioId,
            avaliacao.Nota,
            avaliacao.Titulo,
            avaliacao.Comentario,
            avaliacao.Tags,
            avaliacao.Contexto,
            avaliacao.VotosUteis,
            avaliacao.DataCriacao);

        _avaliacaoRepository.Setup(r => r.ObterPorIdAsync("id-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(avaliacao);

        var act = () => _service.RemoverAsync("id-1", "usuario-1", false);

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task RemoverAsync_DevePermitirAdminRemoverQualquerAvaliacao()
    {
        var avaliacao = new Avaliacao("jogo-1", "usuario-2", 5, "comentário");
        avaliacao = Avaliacao.Restaurar(
            "id-1",
            avaliacao.JogoId,
            avaliacao.UsuarioId,
            avaliacao.Nota,
            avaliacao.Titulo,
            avaliacao.Comentario,
            avaliacao.Tags,
            avaliacao.Contexto,
            avaliacao.VotosUteis,
            avaliacao.DataCriacao);

        _avaliacaoRepository.Setup(r => r.ObterPorIdAsync("id-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(avaliacao);

        await _service.RemoverAsync("id-1", "usuario-1", true);

        _avaliacaoRepository.Verify(r => r.RemoverAsync("id-1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
