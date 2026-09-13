using System.Net;
using Fcg.Catalog.Infrastructure.Settings;
using Microsoft.AspNetCore.HttpOverrides;
// Alias obrigatório: Microsoft.AspNetCore.HttpOverrides também declara um IPNetwork (obsoleto
// desde o .NET 9), então o nome fica ambíguo com os dois namespaces importados. Apontamos
// explicitamente para o tipo do BCL, que é o que KnownIPNetworks espera.
using IPNetwork = System.Net.IPNetwork;

namespace Fcg.Catalog.Api.Extensions;

/// <summary>
/// Processamento de headers <c>X-Forwarded-*</c> para operação atrás do API Gateway.
/// </summary>
public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddGatewayForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(ForwardedHeadersSettings.SectionName)
            .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

        services.Configure<ForwardedHeadersSettings>(
            configuration.GetSection(ForwardedHeadersSettings.SectionName));

        if (!settings.Enabled)
            return services;

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                | ForwardedHeaders.XForwardedProto
                | ForwardedHeaders.XForwardedHost;

            options.ForwardLimit = settings.ForwardLimit;

            // KnownIPNetworks (não KnownNetworks) e System.Net.IPNetwork: os equivalentes do
            // Microsoft.AspNetCore.HttpOverrides estão obsoletos desde o .NET 9 (ASPDEPR005).
            // Limpar os dois é deliberado: o default confia na loopback, e queremos confiar
            // APENAS nas redes declaradas na configuração.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var cidr in settings.KnownNetworks)
            {
                var parts = cidr.Split('/', 2);
                if (parts.Length == 2
                    && IPAddress.TryParse(parts[0], out var prefix)
                    && int.TryParse(parts[1], out var length))
                {
                    options.KnownIPNetworks.Add(new IPNetwork(prefix, length));
                }
            }
        });

        return services;
    }
}
