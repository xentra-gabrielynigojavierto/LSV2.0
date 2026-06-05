using BuildingBlocks.Notifications;
using Flow.Application.Adapters.AuditAdapter;
using Flow.Application.Adapters.NotificationAdapter;
using Flow.Application.Events;
using Flow.Application.Outbox;
using Flow.Infrastructure.Adapters;
using Flow.Infrastructure.Events;
using Flow.Infrastructure.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flow.Infrastructure;

/// <summary>
/// Wires Flow's platform adapter seams. Logging-backed safe defaults are
/// always registered. HTTP-backed clients are layered in only when
/// configuration supplies a base URL, keeping local/dev runs viable
/// without external dependencies.
/// </summary>
public static class PlatformAdapterRegistration
{
    public static IServiceCollection AddFlowPlatformAdapters(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ----- Audit ----------------------------------------------------
        services.AddSingleton<LoggingAuditAdapter>();
        var auditBaseUrl = configuration["Audit:BaseUrl"];

        // LS-FLOW-E13.1 — shared header provider used by BOTH the write
        // and the query adapter so Flow → Audit hops carry the operator's
        // bearer (or a minted service token) instead of being anonymous.
        // IHttpContextAccessor is registered upstream in Flow.Api;
        // IServiceTokenIssuer is optional so DI doesn't fail when the
        // issuer is not registered (e.g. unit tests without auth).
        services.AddHttpContextAccessor();
        services.AddSingleton<AuditAuthHeaderProvider>(sp => new AuditAuthHeaderProvider(
            sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            sp.GetService<BuildingBlocks.Authentication.ServiceTokens.IServiceTokenIssuer>()));

        if (!string.IsNullOrWhiteSpace(auditBaseUrl))
        {
            services.AddHttpClient<HttpAuditAdapter>(client =>
            {
                client.BaseAddress = new Uri(EnsureTrailingSlash(auditBaseUrl));
                client.Timeout = TimeSpan.FromSeconds(5);
            });

            services.AddScoped<IAuditAdapter>(sp => new HttpAuditAdapter(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpAuditAdapter)),
                sp.GetRequiredService<LoggingAuditAdapter>(),
                sp.GetRequiredService<AuditAuthHeaderProvider>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HttpAuditAdapter>>()));
        }
        else
        {
            services.AddScoped<IAuditAdapter>(sp => sp.GetRequiredService<LoggingAuditAdapter>());
        }

        // ----- Audit query (E13.1, read-only) ---------------------------
        // Parallel to the write seam above. Same Audit:BaseUrl toggle:
        // when configured, route through the HTTP adapter with the
        // empty baseline as a graceful fallback; when not, callers see
        // an empty timeline rather than an exception.
        services.AddSingleton<EmptyAuditQueryAdapter>();

        if (!string.IsNullOrWhiteSpace(auditBaseUrl))
        {
            services.AddHttpClient<HttpAuditQueryAdapter>(client =>
            {
                client.BaseAddress = new Uri(EnsureTrailingSlash(auditBaseUrl));
                client.Timeout     = TimeSpan.FromSeconds(10);
            });

            services.AddScoped<IAuditQueryAdapter>(sp => new HttpAuditQueryAdapter(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpAuditQueryAdapter)),
                sp.GetRequiredService<EmptyAuditQueryAdapter>(),
                sp.GetRequiredService<AuditAuthHeaderProvider>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HttpAuditQueryAdapter>>()));
        }
        else
        {
            services.AddScoped<IAuditQueryAdapter>(sp => sp.GetRequiredService<EmptyAuditQueryAdapter>());
        }

        // ----- Notifications --------------------------------------------
        // LS-NOTIF-CORE-021: NotificationsAuthDelegatingHandler mints a service
        // JWT (using Flow's existing IServiceTokenIssuer) before each POST to
        // /v1/notifications so the call is authenticated rather than anonymous.
        // IServiceTokenIssuer is registered in Flow.Api/Program.cs via
        // AddServiceTokenIssuer(config, "flow").
        services.AddSingleton<LoggingNotificationAdapter>();
        services.AddTransient<NotificationsAuthDelegatingHandler>();
        var notifBaseUrl = configuration["Notifications:BaseUrl"];

        if (!string.IsNullOrWhiteSpace(notifBaseUrl))
        {
            services.AddHttpClient<HttpNotificationAdapter>(client =>
            {
                client.BaseAddress = new Uri(EnsureTrailingSlash(notifBaseUrl));
                client.Timeout = TimeSpan.FromSeconds(5);
            })
            .AddHttpMessageHandler<NotificationsAuthDelegatingHandler>();

            services.AddScoped<INotificationAdapter>(sp => new HttpNotificationAdapter(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpNotificationAdapter)),
                sp.GetRequiredService<LoggingNotificationAdapter>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HttpNotificationAdapter>>()));
        }
        else
        {
            services.AddScoped<INotificationAdapter>(sp => sp.GetRequiredService<LoggingNotificationAdapter>());
        }

        // ----- Internal in-process event dispatcher ---------------------
        services.AddScoped<IFlowEventDispatcher, FlowEventDispatcher>();

        // ----- LS-FLOW-E10.2 — transactional outbox + async processor --
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<OutboxDispatcher>();
        services.AddHostedService<OutboxProcessor>();

        // ----- LS-FLOW-E10.3 — workflow SLA / timer evaluator -----------
        services.Configure<WorkflowSlaOptions>(configuration.GetSection(WorkflowSlaOptions.SectionName));
        services.AddHostedService<WorkflowSlaEvaluator>();

        // ----- LS-FLOW-E10.3 (task slice) — task SLA clock + evaluator --
        // Clock is scoped because the factory consuming it is scoped.
        // Evaluator is hosted (singleton lifecycle) and opens its own
        // DI scope per tick, mirroring the workflow-level evaluator.
        services.Configure<WorkflowTaskSlaOptions>(configuration.GetSection(WorkflowTaskSlaOptions.SectionName));
        services.AddScoped<Flow.Application.Interfaces.IWorkflowTaskSlaClock, WorkflowTaskSlaClock>();
        services.AddHostedService<WorkflowTaskSlaEvaluator>();

        return services;
    }

    private static string EnsureTrailingSlash(string url) =>
        url.EndsWith('/') ? url : url + "/";
}
