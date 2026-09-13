using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MongoDb;
using Testcontainers.RabbitMq;

namespace Fcg.Catalog.IntegrationTests.Infrastructure;

/// <summary>
/// Sobe a Catalog API contra um MongoDB (replica set rs0, exigido pelo outbox) e um
/// RabbitMQ reais via Testcontainers.
/// </summary>
public sealed class FcgWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtSecret = "IntegrationTests_HmacSha256_Secret_Key_With_At_Least_32_Chars!";
    public const string JwtIssuer = "FiapCloudGames";
    public const string JwtAudience = "FiapCloudGames";

    private const string RabbitUsername = "guest";
    private const string RabbitPassword = "guest";

    private readonly bool _forwardedHeadersEnabled;
    private readonly string[] _knownNetworks;

    /// <summary>
    /// ÚNICO construtor público — é o que o <c>IClassFixture&lt;T&gt;</c> do xUnit exige.
    /// </summary>
    /// <remarks>
    /// Duas regras do xUnit valem aqui, e as duas já foram quebradas neste arquivo:
    /// <list type="number">
    ///   <item>
    ///     o construtor tem de ser SEM PARÂMETROS — parâmetros com valor default não servem,
    ///     porque a instanciação é por reflexão
    ///     (<i>"had one or more unresolved constructor arguments"</i>);
    ///   </item>
    ///   <item>
    ///     a classe tem de ter UM ÚNICO construtor público — com dois, o xUnit recusa o fixture
    ///     (<i>"may only define a single public constructor"</i>).
    ///   </item>
    /// </list>
    /// Nos dois casos o sintoma é idêntico e engana: a suíte inteira falha em 1 ms, sem executar
    /// nenhum teste. Por isso as variantes de configuração são expostas como MÉTODO DE FÁBRICA
    /// (<see cref="ComForwardedHeaders"/>) em vez de sobrecarga de construtor.
    /// </remarks>
    public FcgWebAppFactory()
    {
        _forwardedHeadersEnabled = false;
        _knownNetworks = [];
    }

    private FcgWebAppFactory(bool forwardedHeadersEnabled, string[] knownNetworks)
    {
        _forwardedHeadersEnabled = forwardedHeadersEnabled;
        _knownNetworks = knownNetworks;
    }

    /// <summary>
    /// App com <c>ForwardedHeaders</c> explicitamente ligado ou desligado, para os testes de
    /// operação atrás do gateway (a configuração é lida no startup, então não dá para reusar o
    /// fixture da classe).
    /// </summary>
    public static FcgWebAppFactory ComForwardedHeaders(bool habilitado, params string[] redesConhecidas) =>
        new(habilitado, redesConhecidas);

    private readonly string _databaseName = $"catalogdb_it_{Guid.NewGuid():N}";
    private string? _mongoConnectionString;

    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7")
        .WithReplicaSet("rs0")
        .Build();

    // Sem bind fixo de porta: usa o mapeamento dinâmico do Testcontainers (evita conflito na 5672
    // com o compose local ou execuções paralelas). A porta é injetada em RabbitMq:Port.
    // Imagem masstransit/rabbitmq: base oficial + plugin rabbitmq_delayed_message_exchange, exigido
    // pelo UseDelayedMessageScheduler/UseDelayedRedelivery (a imagem oficial não traz o plugin).
    // Tag PINADA (linha 3.13, como o orchestration) — evita flakiness por drift do :latest.
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("masstransit/rabbitmq:3.13.1")
        .WithUsername(RabbitUsername)
        .WithPassword(RabbitPassword)
        .WithPortBinding(15672, assignRandomHostPort: true) // Management HTTP (checar a fila _error nos testes)
        .Build();

    /// <summary>Porta HTTP do Management do RabbitMQ (para inspecionar filas, ex.: a _error).</summary>
    public int RabbitManagementPort => _rabbit.GetMappedPublicPort(15672);
    public const string RabbitMgmtUser = RabbitUsername;
    public const string RabbitMgmtPass = RabbitPassword;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();
        await _rabbit.StartAsync();
        _mongoConnectionString = _mongo.GetConnectionString();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("MongoDbSettings:ConnectionString", _mongoConnectionString ?? _mongo.GetConnectionString());
        builder.UseSetting("MongoDbSettings:DatabaseName", _databaseName);
        builder.UseSetting("RabbitMq:Host", "localhost");
        builder.UseSetting("RabbitMq:Port", _rabbit.GetMappedPublicPort(5672).ToString());
        builder.UseSetting("RabbitMq:Username", RabbitUsername);
        builder.UseSetting("RabbitMq:Password", RabbitPassword);
        builder.UseSetting("ForwardedHeaders:Enabled", _forwardedHeadersEnabled ? "true" : "false");
        builder.UseSetting("ForwardedHeaders:ForwardLimit", "1");
        builder.UseSetting("ForwardedHeaders:KnownNetworks:0", _knownNetworks.Length > 0 ? _knownNetworks[0] : "127.0.0.1/32");
        if (_knownNetworks.Length > 1)
        {
            for (var i = 1; i < _knownNetworks.Length; i++)
            {
                builder.UseSetting($"ForwardedHeaders:KnownNetworks:{i}", _knownNetworks[i]);
            }
        }
        // Retry/redelivery CURTOS nos testes: um poison message chega à _error em segundos
        // (em produção os defaults são 3 imediatos + 60/300/900s de redelivery atrasado).
        builder.UseSetting("RabbitMq:ImmediateRetryCount", "1");
        builder.UseSetting("RabbitMq:DelayedRedeliverySeconds", "1,1");
        builder.UseSetting("JwtSettings:SecretKey", JwtSecret);
        builder.UseSetting("JwtSettings:Issuer", JwtIssuer);
        builder.UseSetting("JwtSettings:Audience", JwtAudience);
    }

    /// <summary>Para os containers e libera o host de teste.</summary>
    /// <remarks>
    /// Público de propósito. A implementação de <see cref="IAsyncLifetime.DisposeAsync"/> é
    /// EXPLÍCITA porque o nome colide com o <c>DisposeAsync</c> herdado de
    /// <see cref="WebApplicationFactory{T}"/> (que devolve <c>ValueTask</c>). Consequência
    /// prática: um <c>await using</c> ou um <c>factory.DisposeAsync()</c> direto liga no método
    /// da classe base e NÃO para Mongo nem RabbitMQ — os containers vazam pela execução toda.
    /// Quem instancia a factory à mão deve chamar ESTE método.
    /// </remarks>
    public async Task DisposeContainersAsync()
    {
        // A ORDEM IMPORTA: o host PRIMEIRO, containers depois.
        //
        // Descartar o host para o MassTransit, que fecha canais e conexão AMQP de forma ordenada.
        // Matar o broker antes deixa canais abertos caindo de repente, e o shutdown do
        // RabbitMQ.Client corre para liberar um SemaphoreSlim interno já descartado:
        //
        //   ObjectDisposedException: Object name: 'System.Threading.SemaphoreSlim'
        //      at SemaphoreSlim.Release()
        //      at Channel.MaybeHandlePublisherConfirmationTcsOnChannelShutdownAsync(...)
        //
        // Como isso acontece numa thread do pool e ninguém observa a exceção, ela derruba o HOST
        // DE TESTE inteiro. O sintoma engana: o runner aborta a execução e ainda assim imprime
        // "Passed!" com um total PARCIAL (ex.: 20 de 26), então um filtro que só olhe a linha
        // "Passed!" lê isso como sucesso.
        //
        // A corrida é um BUG CONHECIDO do RabbitMQ.Client 7.1.2 (a versão que o MassTransit 8.5.4
        // traz por transitividade), corrigido na 7.2.0 — upstream PR #1873, "Do not handle
        // publisher confirms when disposed", que altera exatamente este método. Optamos por NÃO
        // pinar a 7.2.x: medido em 6 execuções consecutivas, a ordem correta de descarte elimina
        // o problema, e um override de dependência transitiva é risco sem ganho demonstrado.
        // Se o abort voltar, o pin em Fcg.Catalog.Infrastructure.csproj é o próximo passo.
        await DisposeAsync();
        await _rabbit.DisposeAsync();
        await _mongo.DisposeAsync();
    }

    async Task IAsyncLifetime.DisposeAsync() => await DisposeContainersAsync();
}
