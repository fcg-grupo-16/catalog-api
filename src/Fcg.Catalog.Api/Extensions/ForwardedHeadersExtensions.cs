using System.Net;
using Fcg.Catalog.Infrastructure.Settings;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

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

            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();

            foreach (var cidr in settings.KnownNetworks)
            {
                var parts = cidr.Split('/', 2);
                if (parts.Length == 2
                    && IPAddress.TryParse(parts[0], out var prefix)
                    && int.TryParse(parts[1], out var length))
                {
                    options.KnownNetworks.Add(new IPNetwork(prefix, length));
                }
            }
        });

        return services;
    }
}
