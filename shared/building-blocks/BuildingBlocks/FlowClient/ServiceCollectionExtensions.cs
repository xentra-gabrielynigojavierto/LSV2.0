using BuildingBlocks.Authentication.ServiceTokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.FlowClient;

public static class FlowClientServiceCollectionExtensions
{
    /// <summary>
    /// LS-FLOW-MERGE-P4 — register <see cref="IFlowClient"/> using the
    /// <c>Flow</c> configuration section. Adds <see cref="IHttpContextAccessor"/>
    /// so the client can forward the caller's bearer token.
    ///
    /// LS-FLOW-MERGE-P5 — also registers an <see cref="IServiceTokenIssuer"/>
    /// when <paramref name="serviceName"/> is supplied, so the FlowClient
    /// prefers M2M tokens over user bearer pass-through.
    /// </summary>
    public static IServiceCollection AddFlowClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string? serviceName = null)
    {
        services.AddOptions<FlowClientOptions>()
            .Bind(configuration.GetSection(FlowClientOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Flow:BaseUrl must be configured.")
            .Validate(o => o.TimeoutSeconds > 0, "Flow:TimeoutSeconds must be positive.");

        services.AddHttpContextAccessor();

        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            services.AddServiceTokenIssuer(configuration, serviceName!);
        }

        services.AddTransient<FlowRetryHandler>(sp =>
            new FlowRetryHandler(sp.GetRequiredService<ILogger<FlowRetryHandler>>()));

        services.AddHttpClient<IFlowClient, FlowClient>((sp, http) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FlowClientOptions>>().Value;
            http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/'));
            http.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        })
        .AddHttpMessageHandler<FlowRetryHandler>();

        return services;
    }
}
