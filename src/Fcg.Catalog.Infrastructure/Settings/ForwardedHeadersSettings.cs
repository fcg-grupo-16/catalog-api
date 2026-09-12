namespace Fcg.Catalog.Infrastructure.Settings;

/// <summary>
/// Configuração da confiança em headers <c>X-Forwarded-*</c> quando a API fica atrás de um gateway.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public bool Enabled { get; init; }

    public string[] KnownNetworks { get; init; } = [];

    public int ForwardLimit { get; init; } = 1;
}
