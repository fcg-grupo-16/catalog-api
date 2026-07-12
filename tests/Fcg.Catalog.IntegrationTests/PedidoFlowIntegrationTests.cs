using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fcg.Catalog.IntegrationTests.Infrastructure;
using Fcg.Contracts.Events;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Fcg.Catalog.IntegrationTests;

public class PedidoFlowIntegrationTests(FcgWebAppFactory factory) : IClassFixture<FcgWebAppFactory>
{
    private HttpClient AdminClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.Gerar("admin-1", "Administrador"));
        return client;
    }

    private HttpClient UserClient(string userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.Gerar(userId));
        return client;
    }

    private async Task<string> CriarJogoAsync()
    {
        var admin = AdminClient();
        var resp = await admin.PostAsJsonAsync("/api/v1/jogos", new
        {
            titulo = $"Jogo IT {Guid.NewGuid():N}",
            descricao = "Jogo de teste de integração.",
            genero = 2,
            preco = 49.90m,
            dataLancamento = "2024-01-01T00:00:00Z"
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var jogo = await resp.Content.ReadFromJsonAsync<JogoDto>();
        return jogo!.Id;
    }

    private async Task<string> ComprarAsync(HttpClient user, string gameId)
    {
        var compra = await user.PostAsJsonAsync("/api/v1/biblioteca", new { jogoId = gameId });
        compra.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var aceite = await compra.Content.ReadFromJsonAsync<CompraDto>();
        aceite!.OrderId.Should().NotBeNullOrWhiteSpace();
        return aceite.OrderId;
    }

    [Fact]
    public async Task Compra_DevePersistirPedido_ERetornarOrderIdNoCorpo()
    {
        var gameId = await CriarJogoAsync();
        var user = UserClient("user-compra");

        var orderId = await ComprarAsync(user, gameId);
        Guid.TryParse(orderId, out _).Should().BeTrue();

        var getPedido = await user.GetAsync($"/api/v1/pedidos/{orderId}");
        var corpo = await getPedido.Content.ReadAsStringAsync();
        getPedido.StatusCode.Should().Be(HttpStatusCode.OK, "corpo da resposta: {0}", corpo);

        var pedido = await getPedido.Content.ReadFromJsonAsync<PedidoDto>();
        pedido!.Status.Should().Be("Pending");
        pedido.UserId.Should().Be("user-compra");
        pedido.GameId.Should().Be(gameId);
    }

    [Fact]
    public async Task Pedido_DeOutroUsuario_DeveRetornar403()
    {
        var gameId = await CriarJogoAsync();
        var orderId = await ComprarAsync(UserClient("dono"), gameId);

        var resp = await UserClient("intruso").GetAsync($"/api/v1/pedidos/{orderId}");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PagamentoAprovado_DeveTransicionarPedidoParaApproved()
    {
        var gameId = await CriarJogoAsync();
        var user = UserClient("user-aprovado");
        var orderId = Guid.Parse(await ComprarAsync(user, gameId));

        await PublicarPagamentoAsync(orderId, "user-aprovado", gameId, "Approved");

        await AguardarStatusAsync(user, orderId, "Approved", TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task PagamentoRejeitado_DeveTransicionarPedidoParaRejected()
    {
        var gameId = await CriarJogoAsync();
        var user = UserClient("user-rejeitado");
        var orderId = Guid.Parse(await ComprarAsync(user, gameId));

        await PublicarPagamentoAsync(orderId, "user-rejeitado", gameId, "Rejected");

        await AguardarStatusAsync(user, orderId, "Rejected", TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task PostJogo_SemToken_DeveRetornar401()
    {
        var anonimo = factory.CreateClient(); // sem Authorization header
        var resp = await anonimo.PostAsJsonAsync("/api/v1/jogos", new
        {
            titulo = "Sem token", descricao = "x", genero = 2, preco = 10m,
            dataLancamento = "2024-01-01T00:00:00Z"
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostJogo_ComUsuarioNaoAdmin_DeveRetornar403()
    {
        var user = UserClient("usuario-comum"); // token válido, mas sem role Administrador
        var resp = await user.PostAsJsonAsync("/api/v1/jogos", new
        {
            titulo = "Nao admin", descricao = "x", genero = 2, preco = 10m,
            dataLancamento = "2024-01-01T00:00:00Z"
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PagamentoAprovado_DeveGravarJogoNaBiblioteca()
    {
        var gameId = await CriarJogoAsync();
        var user = UserClient("user-biblioteca-ok");
        var orderId = Guid.Parse(await ComprarAsync(user, gameId));

        await PublicarPagamentoAsync(orderId, "user-biblioteca-ok", gameId, "Approved");
        await AguardarStatusAsync(user, orderId, "Approved", TimeSpan.FromSeconds(30));

        // Side-effect: o jogo aprovado deve aparecer na biblioteca do usuário.
        (await AguardarBibliotecaContemAsync(user, gameId, TimeSpan.FromSeconds(10)))
            .Should().BeTrue("um pagamento aprovado grava o jogo na biblioteca");
    }

    [Fact]
    public async Task PagamentoRejeitado_NaoDeveGravarNaBiblioteca()
    {
        var gameId = await CriarJogoAsync();
        var user = UserClient("user-biblioteca-rejeitado");
        var orderId = Guid.Parse(await ComprarAsync(user, gameId));

        await PublicarPagamentoAsync(orderId, "user-biblioteca-rejeitado", gameId, "Rejected");
        await AguardarStatusAsync(user, orderId, "Rejected", TimeSpan.FromSeconds(30));

        // Side-effect negativo: pagamento rejeitado NÃO grava na biblioteca.
        (await BibliotecaContemAsync(user, gameId))
            .Should().BeFalse("um pagamento rejeitado não deve gravar o jogo na biblioteca");
    }

    private static async Task<bool> BibliotecaContemAsync(HttpClient user, string gameId)
    {
        var resp = await user.GetAsync("/api/v1/biblioteca");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var itens = await resp.Content.ReadFromJsonAsync<List<BibliotecaItemDto>>() ?? [];
        return itens.Any(i => i.Id == gameId);
    }

    private static async Task<bool> AguardarBibliotecaContemAsync(HttpClient user, string gameId, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (await BibliotecaContemAsync(user, gameId))
            {
                return true;
            }
            await Task.Delay(500);
        }
        return false;
    }

    private async Task PublicarPagamentoAsync(Guid orderId, string userId, string gameId, string status)
    {
        var bus = factory.Services.GetRequiredService<IBus>();
        await bus.Publish(new PaymentProcessedEvent
        {
            OrderId = orderId,
            UserId = userId,
            GameId = gameId,
            Price = 49.90m,
            Status = status
        });
    }

    private static async Task AguardarStatusAsync(HttpClient user, Guid orderId, string statusEsperado, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string? ultimo = null;
        while (sw.Elapsed < timeout)
        {
            var resp = await user.GetAsync($"/api/v1/pedidos/{orderId}");
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var pedido = await resp.Content.ReadFromJsonAsync<PedidoDto>();
                ultimo = pedido?.Status;
                if (ultimo == statusEsperado)
                {
                    return;
                }
            }

            await Task.Delay(500);
        }

        throw new Xunit.Sdk.XunitException(
            $"Pedido {orderId} não atingiu o status '{statusEsperado}' em {timeout.TotalSeconds}s (último: '{ultimo}').");
    }

    private sealed record JogoDto(string Id);
    private sealed record CompraDto(string OrderId);
    private sealed record PedidoDto(string OrderId, string UserId, string GameId, decimal Preco, string Status);
    private sealed record BibliotecaItemDto(string Id);
}
