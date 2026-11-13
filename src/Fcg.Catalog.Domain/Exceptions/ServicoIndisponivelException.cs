namespace Fcg.Catalog.Domain.Exceptions;

/// <summary>
/// Uma dependência que a operação exige não está disponível (não configurada ou fora do ar).
/// Vira <c>503 Service Unavailable</c>: o pedido do cliente está certo, quem falhou foi a plataforma.
/// </summary>
public class ServicoIndisponivelException : DomainException
{
    public ServicoIndisponivelException(string message) : base(message) { }

    public ServicoIndisponivelException(string message, Exception innerException) : base(message, innerException) { }
}
