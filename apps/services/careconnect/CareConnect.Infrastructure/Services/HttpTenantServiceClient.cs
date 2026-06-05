// BLK-CC-01: HTTP client that calls the Tenant service for tenant lifecycle operations.
// Replaces the retired Identity tenant endpoints (BLK-ID-01).
//
// Failure policy: ALL infrastructure failures (network, timeout, 5xx, parse) return null.
//                 409 Conflict returns a typed failure result (CODE_TAKEN).
//                 Callers are responsible for surfacing errors — this is NOT a silent fallback.
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CareConnect.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareConnect.Infrastructure.Services;

public sealed class HttpTenantServiceClient : ITenantServiceClient
{
    private readonly IHttpClientFactory          _httpClientFactory;
    private readonly TenantServiceOptions        _options;
    private readonly ILogger<HttpTenantServiceClient> _logger;
    private readonly bool                        _isEnabled;

    public HttpTenantServiceClient(
        IHttpClientFactory               httpClientFactory,
        IOptions<TenantServiceOptions>   options,
        ILogger<HttpTenantServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options           = options.Value;
        _logger            = logger;
        _isEnabled         = !string.IsNullOrWhiteSpace(_options.BaseUrl);

        if (!_isEnabled)
        {
            _logger.LogWarning(
                "BLK-CC-01 TenantService:BaseUrl not configured. " +
                "All Tenant service calls will return null.");
        }
    }

    // ── GET /api/v1/tenants/check-code?code={code} ────────────────────────────

    public async Task<TenantCodeCheckResult?> CheckCodeAsync(
        string            code,
        CancellationToken ct = default)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("BLK-CC-01 CheckCode skipped (BaseUrl not configured) for '{Code}'.", code);
            return null;
        }

        if (string.IsNullOrWhiteSpace(code))
            return null;

        try
        {
            using var client = BuildClient();
            using var cts    = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            using var response = await client.GetAsync(
                $"api/v1/tenants/check-code?code={Uri.EscapeDataString(code)}", cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "BLK-CC-01 CheckCode returned HTTP {Status} for code '{Code}'.",
                    (int)response.StatusCode, code);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<CheckCodeResponse>(
                cancellationToken: cts.Token);

            if (result is null) return null;

            return new TenantCodeCheckResult
            {
                Available      = result.Available,
                NormalizedCode = result.NormalizedCode ?? code,
                Message        = result.Error,
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("BLK-CC-01 CheckCode timed out for '{Code}'.", code);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BLK-CC-01 CheckCode failed for '{Code}'.", code);
            return null;
        }
    }

    // ── POST /api/v1/tenants/provision ────────────────────────────────────────

    public async Task<TenantProvisionResult?> ProvisionAsync(
        string            tenantName,
        string            tenantCode,
        CancellationToken ct = default)
    {
        if (!_isEnabled)
        {
            _logger.LogWarning(
                "BLK-CC-01 ProvisionTenant skipped (BaseUrl not configured) for code '{TenantCode}'.",
                tenantCode);
            return null;
        }

        try
        {
            using var client = BuildClient();
            using var cts    = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(_options.TimeoutSeconds, 30)));

            var body = new { tenantName, tenantCode };

            using var response = await client.PostAsJsonAsync("api/v1/tenants/provision", body, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cts.Token);

                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogWarning(
                        "BLK-CC-01 ProvisionTenant: tenant code '{TenantCode}' already taken (409).",
                        tenantCode);
                    return new TenantProvisionResult
                    {
                        IsSuccess   = false,
                        FailureCode = "CODE_TAKEN",
                    };
                }

                _logger.LogWarning(
                    "BLK-CC-01 ProvisionTenant returned HTTP {Status} for code '{TenantCode}': {Body}",
                    (int)response.StatusCode, tenantCode, errorBody);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<ProvisionResponse>(
                cancellationToken: cts.Token);

            if (result is null || result.TenantId == Guid.Empty)
            {
                _logger.LogWarning(
                    "BLK-CC-01 ProvisionTenant returned null/empty TenantId for code '{TenantCode}'.",
                    tenantCode);
                return null;
            }

            _logger.LogInformation(
                "BLK-CC-01 Tenant '{TenantCode}' provisioned. TenantId={TenantId} Subdomain={Subdomain}.",
                result.TenantCode, result.TenantId, result.Subdomain);

            return new TenantProvisionResult
            {
                TenantId   = result.TenantId,
                TenantCode = result.TenantCode ?? string.Empty,
                Subdomain  = result.Subdomain  ?? string.Empty,
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("BLK-CC-01 ProvisionTenant timed out for code '{TenantCode}'.", tenantCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BLK-CC-01 ProvisionTenant failed for code '{TenantCode}'.", tenantCode);
            return null;
        }
    }

    // ── GET /api/v1/tenants/{id}/subdomain ────────────────────────────────────

    public async Task<string?> GetSubdomainAsync(
        Guid              tenantId,
        CancellationToken ct = default)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("BLK-CC-01 GetSubdomain skipped (BaseUrl not configured) for tenant '{TenantId}'.", tenantId);
            return null;
        }

        try
        {
            using var client = BuildClient();
            using var cts    = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            using var response = await client.GetAsync(
                $"api/v1/tenants/{tenantId}/subdomain", cts.Token);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "BLK-CC-01 GetSubdomain returned HTTP {Status} for tenant '{TenantId}'.",
                    (int)response.StatusCode, tenantId);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<SubdomainResponse>(
                cancellationToken: cts.Token);

            return string.IsNullOrWhiteSpace(result?.Subdomain) ? null : result.Subdomain;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("BLK-CC-01 GetSubdomain timed out for tenant '{TenantId}'.", tenantId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BLK-CC-01 GetSubdomain failed for tenant '{TenantId}'.", tenantId);
            return null;
        }
    }

    // ── Shared HTTP client builder ─────────────────────────────────────────────

    private HttpClient BuildClient()
    {
        var client = _httpClientFactory.CreateClient("TenantService");
        client.BaseAddress = new Uri(_options.BaseUrl!.TrimEnd('/') + "/");
        client.Timeout     = TimeSpan.FromSeconds(Math.Max(_options.TimeoutSeconds, 30));

        if (!string.IsNullOrWhiteSpace(_options.ProvisioningToken))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "X-Provisioning-Token", _options.ProvisioningToken);
        }

        return client;
    }

    // ── Private response models ────────────────────────────────────────────────

    private sealed class CheckCodeResponse
    {
        [JsonPropertyName("available")]
        public bool    Available      { get; set; }

        [JsonPropertyName("normalizedCode")]
        public string? NormalizedCode { get; set; }

        [JsonPropertyName("error")]
        public string? Error          { get; set; }
    }

    private sealed class ProvisionResponse
    {
        [JsonPropertyName("tenantId")]
        public Guid    TenantId  { get; set; }

        [JsonPropertyName("tenantCode")]
        public string? TenantCode { get; set; }

        [JsonPropertyName("subdomain")]
        public string? Subdomain  { get; set; }
    }

    private sealed class SubdomainResponse
    {
        [JsonPropertyName("subdomain")]
        public string? Subdomain { get; set; }
    }
}
