using Fcg.Catalog.Application.Services;
using Fcg.Catalog.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Fcg.Catalog.IntegrationTests;

/// <summary>
/// Testes de REGRESSÃO do cache distribuído.
/// </summary>
/// <remarks>
/// Não testam comportamento de cache (isso está em <c>CacheDecoratorTests</c>); testam o que NÃO
/// pode ser cacheado. Biblioteca e pedidos são dados <b>por usuário</b>: uma chave de cache sem o
/// <c>usuarioId</c> faria o usuário A receber a biblioteca do usuário B — vazamento de dados entre
/// contas, o pior bug possível nesta área. O teste falha no instante em que alguém envolver esses
/// services num decorator de cache, o que é exatamente quando ninguém vai lembrar do risco.
/// </remarks>
[Collection(PlataformaCollection.Nome)]
public sealed class CacheRegressaoTests(FcgWebAppFactory factory)
{
    [Fact(DisplayName = "Biblioteca NÃO é cacheada (dado por usuário)")]
    public void Biblioteca_NaoEhCacheada()
    {
        using var scope = factory.Services.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IBibliotecaService>();

        Assert.IsType<BibliotecaService>(service);
    }

    [Fact(DisplayName = "Status do pedido NÃO é cacheado (é o que o cliente fica pollando)")]
    public void Pedidos_NaoSaoCacheados()
    {
        using var scope = factory.Services.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IPedidoService>();

        Assert.IsType<PedidoService>(service);
    }

    [Fact(DisplayName = "Jogos e avaliações SIM são cacheados (o decorator está no lugar)")]
    public void JogosEAvaliacoes_SaoCacheados()
    {
        using var scope = factory.Services.CreateScope();

        // Contraprova dos dois testes acima: se o registro do DI quebrasse por completo, eles
        // passariam por acidente. Este garante que o decorator realmente está montado.
        Assert.IsType<CachedJogoService>(scope.ServiceProvider.GetRequiredService<IJogoService>());
        Assert.IsType<CachedAvaliacaoService>(scope.ServiceProvider.GetRequiredService<IAvaliacaoService>());
    }
}
