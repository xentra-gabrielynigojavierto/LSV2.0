// BLK-CC-01: HTTP client that calls the Identity service BLK-ID-02 membership APIs.
// Identity service = membership / access only (no tenant creation).
//
// Failure policy: ALL infrastructure failures (network, timeout, 5xx, 401) return null.
//                 Callers must treat null as a hard failure — do NOT silently mark
//                 provider as TENANT if membership assignment fails.
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CareConnect.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareConnect.Infrastructure.Services;

public sealed class HttpIdentityMembershipClient : IIdentityMembershipClient
{
    private readonly IHttpClientFactory                    _httpClientFactory;
    private readonly IdentityServiceOptions                _options;
    private readonly ILogger<HttpIdentityMembershipClient> _logger;
    private readonly bool                                  _isEnabled;

    public HttpIdentityMembershipClient(
        IHttpClientFactory                     httpClientFactory,
        IOptions<IdentityServiceOptions>       options,
        ILogger<HttpIdentityMembershipClient>  logger)
    {
        _httpClientFactory = httpClientFactory;
        _options           = options.Value;
        _logger            = logger;
        _isEnabled         = !string.IsNullOrWhiteSpace(_options.BaseUrl);

        if (!_isEnabled)
        {
            _logger.LogWarning(
                "BLK-CC-01 IdentityService:BaseUrl not configured. " +
                "Identity membership assignment (assign-tenant) will always fail.");
        }
    }

    // ── POST /api/internal/users/assign-tenant ────────────────────────────────

    public async Task<IdentityTenantAssignmentResult?> AssignTenantAsync(
        Guid              userId,
        Guid              tenantId,
        IList<string>     roles,
        CancellationToken ct = default)
    {
        if (!_isEnabled)
        {
            _logger.LogWarning(
                "BLK-CC-01 AssignTenant skipped (BaseUrl not configured) for user {UserId}.", userId);
            return null;
        }

        try
        {
            using var client = BuildClient();
            using var cts    = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var body = new
            {
                userId,
                tenantId,
                roles,
            };

            using var response = await client.PostAsJsonAsync(
                "api/internal/users/assign-tenant", body, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.LogWarning(
                    "BLK-CC-01 AssignTenant returned HTTP {Status} for user {UserId} tenant {TenantId}: {Body}",
                    (int)response.StatusCode, userId, tenantId, errorBody);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<AssignTenantResponse>(
                cancellationToken: cts.Token);

            if (result is null)
            {
                _logger.LogWarning(
                    "BLK-CC-01 AssignTenant returned null body for user {UserId} tenant {TenantId}.",
                    userId, tenantId);
                return null;
            }

            _logger.LogInformation(
                "BLK-CC-01 AssignTenant succeeded for user {UserId} → tenant {TenantId}. " +
                "AlreadyInTenant={AlreadyInTenant}.",
                userId, tenantId, result.AlreadyInTenant);

            return new IdentityTenantAssignmentResult
            {
                UserId          = result.UserId,
                TenantId        = result.TenantId,
                AlreadyInTenant = result.AlreadyInTenant,
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "BLK-CC-01 AssignTenant timed out for user {UserId} tenant {TenantId}.", userId, tenantId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "BLK-CC-01 AssignTenant failed for user {UserId} tenant {TenantId}.", userId, tenantId);
            return null;
        }
    }

    // ── Shared HTTP client builder ─────────────────────────────────────────────
    //
    // BLK-SEC-01: Token injection standardised — always sends X-Provisioning-Token
    // when ProvisioningToken is configured. Mirrors HttpTenantServiceClient pattern.

    private HttpClient BuildClient()
    {
        var client = _httpClientFactory.CreateClient("IdentityService");
        client.BaseAddress = new Uri(_options.BaseUrl!.TrimEnd('/') + "/");
        client.Timeout     = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        // BLK-SEC-01: Use explicit ProvisioningToken (takes precedence over legacy AuthHeaderName/Value).
        if (!string.IsNullOrWhiteSpace(_options.ProvisioningToken))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "X-Provisioning-Token", _options.ProvisioningToken);
        }
        else if (!string.IsNullOrWhiteSpace(_options.AuthHeaderName) &&
                 !string.IsNullOrWhiteSpace(_options.AuthHeaderValue))
        {
            // Legacy fallback — retained for backward compatibility.
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                _options.AuthHeaderName, _options.AuthHeaderValue);
        }

        return client;
    }

    // ── Private response models ────────────────────────────────────────────────

    private sealed class AssignTenantResponse
    {
        [JsonPropertyName("userId")]
        public Guid UserId          { get; set; }

        [JsonPropertyName("tenantId")]
        public Guid TenantId        { get; set; }

        [JsonPropertyName("alreadyInTenant")]
        public bool AlreadyInTenant { get; set; }
    }
}
