using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using BuildingBlocks.Authentication.ServiceTokens;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.FlowClient;

/// <summary>
/// LS-FLOW-MERGE-P4 — typed <see cref="HttpClient"/> implementation of
/// <see cref="IFlowClient"/>. Forwards the caller's <c>Authorization</c>
/// header so Flow's per-product capability policies still apply, logs at
/// the boundary, and surfaces transport failures as
/// <see cref="FlowClientUnavailableException"/>.
///
/// LS-FLOW-MERGE-P5 — when an <see cref="IServiceTokenIssuer"/> is
/// registered the client prefers a freshly-minted M2M token (with the
/// caller's user id forwarded as the <c>actor</c> claim) over the user's
/// bearer for execution-surface calls. Pass-through bearer remains the
/// fallback so dev/test environments without a configured secret keep
/// working.
/// </summary>
internal sealed class FlowClient : IFlowClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceTokenIssuer? _serviceTokens;
    private readonly ILogger<FlowClient> _logger;

    public FlowClient(
        HttpClient http,
        IHttpContextAccessor httpContextAccessor,
        ILogger<FlowClient> logger,
        IServiceTokenIssuer? serviceTokens = null)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
        _serviceTokens = serviceTokens;
        _logger = logger;
    }

    public async Task<FlowProductWorkflowResponse> StartWorkflowAsync(
        string productSlug,
        StartProductWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var path = $"/api/v1/product-workflows/{productSlug}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        ApplyUserBearer(httpRequest);

        _logger.LogInformation(
            "FlowClient → POST {Path} entity={EntityType}/{EntityId} definition={DefinitionId}",
            path, request.SourceEntityType, request.SourceEntityId, request.WorkflowDefinitionId);

        var response = await SendAsync(httpRequest, cancellationToken);
        var dto = await ReadJsonAsync<FlowProductWorkflowResponse>(response, cancellationToken);
        if (dto is null)
        {
            throw new FlowClientUnavailableException("Flow returned an empty body for StartWorkflow.");
        }
        return dto;
    }

    public async Task<IReadOnlyList<FlowProductWorkflowResponse>> ListBySourceEntityAsync(
        string productSlug,
        string sourceEntityType,
        string sourceEntityId,
        CancellationToken cancellationToken = default)
    {
        var qs = $"?sourceEntityType={Uri.EscapeDataString(sourceEntityType)}&sourceEntityId={Uri.EscapeDataString(sourceEntityId)}";
        var path = $"/api/v1/product-workflows/{productSlug}{qs}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyUserBearer(httpRequest);

        _logger.LogInformation(
            "FlowClient → GET {Path} entity={EntityType}/{EntityId}",
            path, sourceEntityType, sourceEntityId);

        var response = await SendAsync(httpRequest, cancellationToken);
        var list = await ReadJsonAsync<List<FlowProductWorkflowResponse>>(response, cancellationToken);
        return list ?? new List<FlowProductWorkflowResponse>();
    }

    public async Task<IReadOnlyList<FlowWorkflowDefinitionResponse>> ListDefinitionsAsync(
        string productKey,
        CancellationToken cancellationToken = default)
    {
        var qs = string.IsNullOrWhiteSpace(productKey)
            ? string.Empty
            : $"?productKey={Uri.EscapeDataString(productKey)}";
        var path = $"/api/v1/workflows{qs}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyUserBearer(httpRequest);

        _logger.LogInformation("FlowClient → GET {Path} productKey={ProductKey}", path, productKey);

        var response = await SendAsync(httpRequest, cancellationToken);
        // Flow returns the full WorkflowDefinitionResponse; deserialize loosely
        // and project to the slim DTO the UI needs.
        var raw = await ReadJsonAsync<List<FlowDefinitionWire>>(response, cancellationToken);
        if (raw is null)
        {
            return Array.Empty<FlowWorkflowDefinitionResponse>();
        }

        var slim = new List<FlowWorkflowDefinitionResponse>(raw.Count);
        foreach (var d in raw)
        {
            slim.Add(new FlowWorkflowDefinitionResponse
            {
                Id = d.Id,
                Name = d.Name ?? string.Empty,
                Description = d.Description,
                Version = d.Version ?? string.Empty,
                Status = d.Status?.ToString() ?? string.Empty,
                ProductKey = d.ProductKey ?? string.Empty,
            });
        }
        return slim;
    }

    private sealed class FlowDefinitionWire
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        public object? Status { get; set; }
        public string? ProductKey { get; set; }
    }

    public async Task<FlowWorkflowInstanceResponse> GetWorkflowInstanceAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        var path = $"/api/v1/workflow-instances/{workflowInstanceId}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyExecutionAuth(httpRequest);

        _logger.LogInformation("FlowClient → GET {Path}", path);

        var response = await SendAsync(httpRequest, cancellationToken);
        var dto = await ReadJsonAsync<FlowWorkflowInstanceResponse>(response, cancellationToken)
                  ?? throw new FlowClientUnavailableException("Flow returned an empty body for GetWorkflowInstance.");
        return dto;
    }

    public async Task<FlowWorkflowInstanceResponse> AdvanceWorkflowAsync(
        Guid workflowInstanceId,
        FlowAdvanceWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var path = $"/api/v1/workflow-instances/{workflowInstanceId}/advance";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        ApplyExecutionAuth(httpRequest);

        _logger.LogInformation(
            "FlowClient → POST {Path} from={From} to={To}",
            path, request.ExpectedCurrentStepKey, request.ToStepKey);

        var response = await SendAsync(httpRequest, cancellationToken);
        var dto = await ReadJsonAsync<FlowWorkflowInstanceResponse>(response, cancellationToken)
                  ?? throw new FlowClientUnavailableException("Flow returned an empty body for AdvanceWorkflow.");
        return dto;
    }

    public async Task<FlowWorkflowInstanceResponse> CompleteWorkflowAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        var path = $"/api/v1/workflow-instances/{workflowInstanceId}/complete";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path);
        ApplyExecutionAuth(httpRequest);

        _logger.LogInformation("FlowClient → POST {Path}", path);

        var response = await SendAsync(httpRequest, cancellationToken);
        var dto = await ReadJsonAsync<FlowWorkflowInstanceResponse>(response, cancellationToken)
                  ?? throw new FlowClientUnavailableException("Flow returned an empty body for CompleteWorkflow.");
        return dto;
    }

    // ------------------ LS-FLOW-HARDEN-A1 — atomic ownership surface ------------------

    public async Task<FlowWorkflowInstanceResponse> GetProductWorkflowAsync(
        string productSlug,
        string sourceEntityType,
        string sourceEntityId,
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        var path = BuildOwnedPath(productSlug, sourceEntityType, sourceEntityId, workflowInstanceId);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyExecutionAuth(httpRequest);

        _logger.LogInformation("FlowClient → GET {Path}", path);

        var response = await SendAsync(httpRequest, cancellationToken);
        return await ReadJsonAsync<FlowWorkflowInstanceResponse>(response, cancellationToken)
               ?? throw new FlowClientUnavailableException("Flow returned an empty body for GetProductWorkflow.");
    }

    public async Task<FlowWorkflowInstanceResponse> AdvanceProductWorkflowAsync(
        string productSlug,
        string sourceEntityType,
        string sourceEntityId,
        Guid workflowInstanceId,
        FlowAdvanceWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var path = BuildOwnedPath(productSlug, sourceEntityType, sourceEntityId, workflowInstanceId) + "/advance";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        ApplyExecutionAuth(httpRequest);

        _logger.LogInformation(
            "FlowClient → POST {Path} from={From} to={To}",
            path, request.ExpectedCurrentStepKey, request.ToStepKey);

        var response = await SendAsync(httpRequest, cancellationToken);
        return await ReadJsonAsync<FlowWorkflowInstanceResponse>(response, cancellationToken)
               ?? throw new FlowClientUnavailableException("Flow returned an empty body for AdvanceProductWorkflow.");
    }

    public async Task<FlowWorkflowInstanceResponse> CompleteProductWorkflowAsync(
        string productSlug,
        string sourceEntityType,
        string sourceEntityId,
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        var path = BuildOwnedPath(productSlug, sourceEntityType, sourceEntityId, workflowInstanceId) + "/complete";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path);
        ApplyExecutionAuth(httpRequest);

        _logger.LogInformation("FlowClient → POST {Path}", path);

        var response = await SendAsync(httpRequest, cancellationToken);
        return await ReadJsonAsync<FlowWorkflowInstanceResponse>(response, cancellationToken)
               ?? throw new FlowClientUnavailableException("Flow returned an empty body for CompleteProductWorkflow.");
    }

    private static string BuildOwnedPath(
        string productSlug, string sourceEntityType, string sourceEntityId, Guid workflowInstanceId)
        => "/api/v1/product-workflows/" +
           $"{Uri.EscapeDataString(productSlug)}/" +
           $"{Uri.EscapeDataString(sourceEntityType)}/" +
           $"{Uri.EscapeDataString(sourceEntityId)}/" +
           $"{workflowInstanceId}";

    /// <summary>
    /// LS-FLOW-MERGE-P5 — auth for the execution surface
    /// (<c>/api/v1/workflow-instances/...</c>). Prefer a fresh service
    /// token (with the caller's user id forwarded as <c>actor</c>) when
    /// the issuer is configured and the caller has a tenant claim; fall
    /// back to bearer pass-through.
    /// </summary>
    private void ApplyExecutionAuth(HttpRequestMessage httpRequest)
    {
        var ctx = _httpContextAccessor.HttpContext;

        if (_serviceTokens is not null && _serviceTokens.IsConfigured)
        {
            var (tenantId, userId) = ExtractTenantAndUser(ctx);
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                var token = _serviceTokens.IssueToken(tenantId!, userId);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return;
            }
        }
        ApplyUserBearer(httpRequest);
    }

    /// <summary>
    /// LS-FLOW-MERGE-P4 — auth for product-workflow management calls
    /// (<c>/api/v1/product-workflows/...</c>). These endpoints enforce
    /// per-product capability policies that service tokens cannot
    /// satisfy, so this path always uses the caller's user bearer.
    /// </summary>
    private void ApplyUserBearer(HttpRequestMessage httpRequest)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null) return;
        var auth = ctx.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(auth)) return;

        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", auth.Substring("Bearer ".Length).Trim());
        }
    }

    private static (string? TenantId, string? UserId) ExtractTenantAndUser(HttpContext? ctx)
    {
        if (ctx?.User is not ClaimsPrincipal user || user.Identity?.IsAuthenticated != true)
            return (null, null);

        var tenantId = user.FindFirst("tenant_id")?.Value
                       ?? user.FindFirst("tid")?.Value;
        var userId   = user.FindFirst("sub")?.Value
                       ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return (tenantId, userId);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "FlowClient ← {Status} {Method} {Uri} body={Body}",
                    (int)response.StatusCode, request.Method, request.RequestUri, Truncate(body, 512));

                // 4xx propagates the upstream HTTP error so policy denials
                // (401/403) and validation (400/409) reach the caller meaningfully.
                throw new HttpRequestException(
                    $"Flow returned {(int)response.StatusCode} for {request.Method} {request.RequestUri}: {Truncate(body, 256)}",
                    inner: null,
                    statusCode: response.StatusCode);
            }
            return response;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            _logger.LogError(ex, "FlowClient transport failure for {Method} {Uri}", request.Method, request.RequestUri);
            throw new FlowClientUnavailableException("Flow service is unreachable.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "FlowClient timeout for {Method} {Uri}", request.Method, request.RequestUri);
            throw new FlowClientUnavailableException("Flow service request timed out.", ex);
        }
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (JsonException ex)
        {
            throw new FlowClientUnavailableException("Flow returned a malformed response body.", ex);
        }
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value.Substring(0, max) + "…";
}
