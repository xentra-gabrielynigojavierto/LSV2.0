using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Authentication.ServiceTokens;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Task.Infrastructure.Services;

/// <summary>
/// TASK-B05 (TASK-015) — registers the Task service in the platform Monitoring
/// service on startup. Fires once, logs the result, then exits. Failures are
/// logged at Warning level and never prevent startup.
/// </summary>
public sealed class TaskServiceRegistrar : IHostedService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
    };

    private const string ServiceEntityName = "Task Service";
    private const string EntityTypeService  = "Service";
    private const string MonitoringTypeHttp = "HttpEndpoint";
    private const string HealthPath         = "/health";

    private readonly TaskMonitoringOptions           _options;
    private readonly IServiceTokenIssuer             _tokenIssuer;
    private readonly IHttpClientFactory              _httpClientFactory;
    private readonly ILogger<TaskServiceRegistrar>   _logger;

    public TaskServiceRegistrar(
        IOptions<TaskMonitoringOptions>  options,
        IServiceTokenIssuer              tokenIssuer,
        IHttpClientFactory               httpClientFactory,
        ILogger<TaskServiceRegistrar>    logger)
    {
        _options           = options.Value;
        _tokenIssuer       = tokenIssuer;
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    public async System.Threading.Tasks.Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogDebug(
                "[TaskMonitor] MonitoringService:BaseUrl not configured — registration skipped.");
            return;
        }

        try
        {
            await RegisterAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[TaskMonitor] Could not register Task service in Monitoring — " +
                "service will still start normally.");
        }
    }

    public System.Threading.Tasks.Task StopAsync(CancellationToken cancellationToken)
        => System.Threading.Tasks.Task.CompletedTask;

    private async System.Threading.Tasks.Task RegisterAsync(CancellationToken ct)
    {
        // Build the health endpoint target URL.
        // We use the ASPNETCORE_URLS / configured URL if available, otherwise fall back
        // to reporting the Monitoring base URL itself as the target.
        var target = Environment.GetEnvironmentVariable("TASK_SERVICE_URL")
                     ?? "http://task:8080";
        var healthTarget = target.TrimEnd('/') + HealthPath;

        var body = new
        {
            name           = ServiceEntityName,
            entityType     = EntityTypeService,
            monitoringType = MonitoringTypeHttp,
            target         = healthTarget,
            isEnabled      = true,
        };

        using var client = _httpClientFactory.CreateClient("TaskMonitoringRegistrar");
        client.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout     = TimeSpan.FromSeconds(Math.Max(_options.TimeoutSeconds, 5));

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/entities");
        request.Content = JsonContent.Create(body, options: JsonOpts);

        // Service token with platform sentinel tenant (monitoring is tenant-agnostic)
        if (_tokenIssuer.IsConfigured)
        {
            var token = _tokenIssuer.IssueToken(
                tenantId: "00000000-0000-0000-0000-000000000001");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogInformation(
                "[TaskMonitor] Task service already registered in Monitoring (409 Conflict — idempotent).");
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body2 = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "[TaskMonitor] Monitoring registration returned {StatusCode}. Body: {Body}",
                (int)response.StatusCode,
                body2.Length > 300 ? body2[..300] : body2);
            return;
        }

        _logger.LogInformation(
            "[TaskMonitor] Task service registered in Monitoring at target={Target}.",
            healthTarget);
    }
}
