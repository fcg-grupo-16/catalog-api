using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Interfaces;
using Fcg.Catalog.Application.Services;
using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Enums;
using Fcg.Catalog.Domain.Repositories;
using Fcg.Catalog.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Fcg.Catalog.UnitTests.Services;

public class CacheDecoratorTests
{
    [Fact]
    public async Task Listar_GenerosDiferentes_UsamChavesDiferentes()
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetGenerationAsync("jogos", It.IsAny<CancellationToken>())).ReturnsAsync(7L);
        cache.Setup(c => c.GetAsync<PaginacaoResponseDto<JogoResponseDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaginacaoResponseDto<JogoResponseDto>?)null);

        var inner = new Mock<IJogoService>();
        inner.Setup(s => s.ListarAsync(1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginacaoResponseDto<JogoResponseDto>(new[] { new JogoResponseDto("j1", "J1", "D", GeneroJogo.Acao, 10m, "BRL", DateTime.UtcNow, true) }, 1, 10, 1));

        var service = new CachedJogoService(inner.Object, cache.Object);

        await service.ListarAsync(1, 10, null);
        await service.ListarAsync(1, 10, GeneroJogo.RPG);

        cache.Verify(c => c.GetAsync<PaginacaoResponseDto<JogoResponseDto>>("jogos:lista:g7:p1:t10:gentodos", It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.GetAsync<PaginacaoResponseDto<JogoResponseDto>>("jogos:lista:g7:p1:t10:genRPG", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InserirLote_InvalidaOGrupoUmaVez()
    {
        var cache = new Mock<ICacheService>();
        var inner = new Mock<IJogoService>();
        inner.Setup(s => s.InserirLoteAsync(It.IsAny<List<CriarJogoRequestDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JogoResponseDto>());

        var service = new CachedJogoService(inner.Object, cache.Object);

        await service.InserirLoteAsync(new List<CriarJogoRequestDto> { new("J1", "D", GeneroJogo.RPG, 10m, DateTime.UtcNow) });

        cache.Verify(c => c.InvalidateGroupAsync("jogos", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Resumo_CacheHit_NaoRodaAAgregacao()
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetAsync<AvaliacaoResumoResponseDto>("avaliacoes:resumo:j1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AvaliacaoResumoResponseDto("j1", 3, 4.5m, new Dictionary<int, long> { [5] = 3 }));

        var inner = new Mock<IAvaliacaoService>();
        var service = new CachedAvaliacaoService(inner.Object, cache.Object);

        var resultado = await service.ObterResumoAsync("j1");

        resultado.Total.Should().Be(3);
        inner.Verify(s => s.ObterResumoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CriarAvaliacao_InvalidaResumoEFeedDaquelesJogo()
    {
        var cache = new Mock<ICacheService>();
        var inner = new Mock<IAvaliacaoService>();
        inner.Setup(s => s.CriarAsync("usuario-1", It.IsAny<CriarAvaliacaoRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AvaliacaoResponseDto("a1", "j1", "usuario-1", 5, null, "ok", [], new Dictionary<string, string>(), 0, DateTime.UtcNow));

        var service = new CachedAvaliacaoService(inner.Object, cache.Object);

        await service.CriarAsync("usuario-1", new CriarAvaliacaoRequestDto("j1", 5, "ok"));

        cache.Verify(c => c.RemoveAsync("avaliacoes:resumo:j1", It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.InvalidateGroupAsync("avaliacoes:j1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CriarAvaliacaoNoJogoA_NaoInvalidaOJogoB()
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetGenerationAsync("avaliacoes:j1", It.IsAny<CancellationToken>())).ReturnsAsync(1L);
        cache.Setup(c => c.GetGenerationAsync("avaliacoes:j2", It.IsAny<CancellationToken>())).ReturnsAsync(2L);

        var inner = new Mock<IAvaliacaoService>();
        inner.Setup(s => s.CriarAsync("usuario-1", It.IsAny<CriarAvaliacaoRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AvaliacaoResponseDto("a1", "j1", "usuario-1", 5, null, "ok", [], new Dictionary<string, string>(), 0, DateTime.UtcNow));

        var service = new CachedAvaliacaoService(inner.Object, cache.Object);

        await service.CriarAsync("usuario-1", new CriarAvaliacaoRequestDto("j1", 5, "ok"));

        cache.Verify(c => c.InvalidateGroupAsync("avaliacoes:j1", It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.InvalidateGroupAsync("avaliacoes:j2", It.IsAny<CancellationToken>()), Times.Never);
    }
}
