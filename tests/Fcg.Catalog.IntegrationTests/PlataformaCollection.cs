using Fcg.Catalog.IntegrationTests.Infrastructure;

namespace Fcg.Catalog.IntegrationTests;

/// <summary>
/// Collection única da suíte de integração: UM MongoDB e UM RabbitMQ para todas as classes.
/// </summary>
/// <remarks>
/// <para>
/// Por default, o xUnit trata cada classe de teste como a sua própria collection e roda
/// collections <b>em paralelo</b>. Com quatro classes usando <c>IClassFixture&lt;FcgWebAppFactory&gt;</c>,
/// a suíte subia <b>4 MongoDB (replica set) + 4 RabbitMQ simultâneos</b> — e quebrava de forma
/// não determinística, com falhas trocando de teste a cada execução:
/// </para>
/// <list type="bullet">
///   <item><c>Docker API responded with status code='Conflict' ... container is not running</c></item>
///   <item><c>InvalidOperationException: The entry point exited without ever building an IHost</c>
///   (o host não sobe porque o container de que ele depende morreu)</item>
/// </list>
/// <para>
/// Uma <c>ICollectionFixture</c> compartilhada resolve as duas coisas: os containers passam a ser
/// <b>um par para a suíte inteira</b>, e as classes rodam em série (não há paralelismo dentro de
/// uma collection), o que torna o resultado reprodutível.
/// </para>
/// <para>
/// <b>Ao adicionar uma classe de teste de integração</b>, marque-a com
/// <c>[Collection(PlataformaCollection.Nome)]</c> e receba a <see cref="FcgWebAppFactory"/> pelo
/// construtor. Não use <c>IClassFixture</c>: cada uso volta a somar um par de containers.
/// Os testes compartilham o mesmo banco, então use identificadores próprios (userId, título de
/// jogo) em vez de depender do estado da collection.
/// </para>
/// </remarks>
[CollectionDefinition(Nome)]
public sealed class PlataformaCollection : ICollectionFixture<FcgWebAppFactory>
{
    public const string Nome = "plataforma-fcg";
}
