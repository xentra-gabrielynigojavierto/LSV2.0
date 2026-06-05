using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LegalSynq.AuditClient;

/// <summary>
/// DI registration for the audit event client.
///
/// In the producer service's Program.cs:
///   builder.Services.AddAuditEventClient(builder.Configuration);
///
/// In appsettings.json of the producer service:
///   "AuditClient": {
///     "BaseUrl":        "http://localhost:5007",
///     "ServiceToken":   "",
///     "SourceSystem":   "identity-service",
///     "SourceService":  "auth-api",
///     "TimeoutSeconds": 5
///   }
/// </summary>
public static class AuditClientServiceCollectionExtensions
{
    public static IServiceCollection AddAuditEventClient(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        services.Configure<AuditClientOptions>(
            configuration.GetSection(AuditClientOptions.SectionName));

        services.AddHttpClient<IAuditEventClient, HttpAuditEventClient>("AuditEventClient");

        return services;
    }
}
