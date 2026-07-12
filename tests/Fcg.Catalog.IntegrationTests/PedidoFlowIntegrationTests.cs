using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

    [Fact]
    public async Task PagamentoDuplicado_DeveAplicarEfeitoUmaVez()
    {
        var gameId = await CriarJogoAsync();
        var user = UserClient("user-dedup");
        var orderId = Guid.Parse(await ComprarAsync(user, gameId));

        var bus = factory.Services.GetRequiredService<IBus>();
        var msgId = NewId.NextGuid();
        var evt = new PaymentProcessedEvent
        {
            OrderId = orderId, UserId = "user-dedup", GameId = gameId, Price = 49.90m, Status = "Approved"
        };
        // Mesmo MessageId entregue 2x: o inbox (UseMongoDbOutbox) deduplica a 2ª entrega.
        await bus.Publish(evt, c => c.MessageId = msgId);
        await bus.Publish(evt, c => c.MessageId = msgId);

        await AguardarStatusAsync(user, orderId, "Approved", TimeSpan.FromSeconds(30));
        // O status vira Approved antes da gravação na biblioteca — aguarda o side-effect aparecer
        // (evita flakiness) antes de contar.
        (await AguardarBibliotecaContemAsync(user, gameId, TimeSpan.FromSeconds(10)))
            .Should().BeTrue("o pagamento aprovado grava o jogo na biblioteca");

        // Efeito exatamente-uma-vez: o jogo aparece na biblioteca uma única vez.
        using var resp = await user.GetAsync("/api/v1/biblioteca");
        var itens = await resp.Content.ReadFromJsonAsync<List<BibliotecaItemDto>>() ?? [];
        itens.Count(i => i.Id == gameId).Should().Be(1, "entrega duplicada não pode gravar o jogo duas vezes");
    }

    [Fact]
    public async Task PagamentoParaPedidoInexistente_DeveIrParaDeadLetter()
    {
        // OrderId que não corresponde a nenhum pedido → o consumer lança (não-DomainException),
        // esgota o retry imediato + delayed redelivery (curtos nos testes) e a mensagem vai para a _error.
        var bus = factory.Services.GetRequiredService<IBus>();
        await bus.Publish(new PaymentProcessedEvent
        {
            OrderId = Guid.NewGuid(), UserId = "fantasma", GameId = Guid.NewGuid().ToString(),
            Price = 10m, Status = "Approved"
        });

        var mensagens = await ContarMensagensNaFilaAsync("catalog-payment-processed_error", TimeSpan.FromSeconds(40));

        mensagens.Should().BeGreaterThan(0,
            "após esgotar retry imediato + delayed redelivery, o poison message deve ir para a fila _error (dead-letter)");
    }

    // Consulta o Management HTTP do RabbitMQ até a fila ter mensagens (ou expirar o timeout).
    private async Task<int> ContarMensagensNaFilaAsync(string fila, TimeSpan timeout)
    {
        using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{factory.RabbitManagementPort}") };
        var auth = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{FcgWebAppFactory.RabbitMgmtUser}:{FcgWebAppFactory.RabbitMgmtPass}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            using var resp = await http.GetAsync($"/api/queues/%2F/{fila}");
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("messages", out var m) && m.GetInt32() > 0)
                {
                    return m.GetInt32();
                }
            }
            await Task.Delay(500);
        }
        return 0;
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
            using var resp = await user.GetAsync($"/api/v1/pedidos/{orderId}");
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
