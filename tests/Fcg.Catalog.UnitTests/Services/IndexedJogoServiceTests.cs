using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.DTOs.Response;
using Fcg.Catalog.Application.Interfaces;
using Fcg.Catalog.Application.Services;
using Fcg.Catalog.Domain.Enums;
using FluentAssertions;
using Fcg.Catalog.Infrastructure.Extensions;
using Fcg.Catalog.Infrastructure.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fcg.Catalog.UnitTests.Services;

public sealed class IndexedJogoServiceTests
{
    private static readonly JogoResponseDto Jogo =
        new("jogo-1", "CodeQuest", "Descricao", GeneroJogo.RPG, 19.90m, "BRL", DateTime.UtcNow, true);

    [Fact]
    public async Task Criar_IndexaDepoisDeDelegar()
    {
        var inner = new Mock<IJogoService>();
        var indice = new Mock<IIndiceDeJogos>();
        inner.Setup(service => service.CriarAsync(It.IsAny<CriarJogoRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Jogo);
        var service = CriarService(inner, indice);

        var resultado = await service.CriarAsync(new CriarJogoRequestDto(
            Jogo.Titulo, Jogo.Descricao, Jogo.Genero, Jogo.Preco, Jogo.DataLancamento));

        resultado.Should().Be(Jogo);
        indice.Verify(index => index.IndexarAsync(Jogo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InserirLote_IndexaEmUmaOperacaoBulk()
    {
        var criados = new[] { Jogo, Jogo with { Id = "jogo-2", Titulo = "Outro jogo" } };
        var inner = new Mock<IJogoService>();
        var indice = new Mock<IIndiceDeJogos>();
        inner.Setup(service => service.InserirLoteAsync(
                It.IsAny<List<CriarJogoRequestDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(criados);
        var service = CriarService(inner, indice);

        await service.InserirLoteAsync([]);

        indice.Verify(index => index.IndexarLoteAsync(
            It.Is<IEnumerable<JogoResponseDto>>(jogos => jogos.Count() == 2),
            It.IsAny<CancellationToken>()), Times.Once);
        indice.Verify(index => index.IndexarAsync(It.IsAny<JogoResponseDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AtualizarIndexaEExcluirRemoveDoIndice()
    {
        var inner = new Mock<IJogoService>();
        var indice = new Mock<IIndiceDeJogos>();
        inner.Setup(service => service.AtualizarAsync("jogo-1", It.IsAny<AtualizarJogoRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Jogo with { Titulo = "Titulo atualizado" });
        var service = CriarService(inner, indice);

        await service.AtualizarAsync("jogo-1", new AtualizarJogoRequestDto(
            "Titulo atualizado", Jogo.Descricao, Jogo.Genero, Jogo.Preco));
        await service.RemoverAsync("jogo-1");

        indice.Verify(index => index.IndexarAsync(
            It.Is<JogoResponseDto>(jogo => jogo.Titulo == "Titulo atualizado"),
            It.IsAny<CancellationToken>()), Times.Once);
        indice.Verify(index => index.RemoverAsync("jogo-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FalhaDeIndexacaoNaoCancelaCriacaoJaPersistida()
    {
        var inner = new Mock<IJogoService>();
        var indice = new Mock<IIndiceDeJogos>();
        var logger = new Mock<ILogger<IndexedJogoService>>();
        inner.Setup(service => service.CriarAsync(It.IsAny<CriarJogoRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Jogo);
        indice.Setup(index => index.IndexarAsync(Jogo, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("OpenSearch indisponivel"));
        var service = new IndexedJogoService(inner.Object, indice.Object, logger.Object);

        var resultado = await service.CriarAsync(new CriarJogoRequestDto(
            Jogo.Titulo, Jogo.Descricao, Jogo.Genero, Jogo.Preco, Jogo.DataLancamento));

        resultado.Should().Be(Jogo);
        logger.Verify(
            log => log.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("não foi indexado")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfiguracaoAusente_DesabilitaIndiceSemFalharNaResolucao()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var indice = provider.GetRequiredService<IIndiceDeJogos>();

        Assert.IsType<IndiceDeJogosIndisponivel>(indice);
        await Assert.ThrowsAsync<Fcg.Catalog.Domain.Exceptions.ServicoIndisponivelException>(() => indice.GarantirIndiceAsync());
    }

    private static IndexedJogoService CriarService(
        Mock<IJogoService> inner,
        Mock<IIndiceDeJogos> indice) =>
        new(inner.Object, indice.Object, Mock.Of<ILogger<IndexedJogoService>>());
}
