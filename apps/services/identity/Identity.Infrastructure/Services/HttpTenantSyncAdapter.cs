using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

/// <summary>
/// TENANT-B07 — Real HTTP implementation of the Identity-side ITenantSyncAdapter.
///
/// Calls POST /api/internal/tenant-sync/upsert on the Tenant service.
/// Registered when Features:TenantDualWriteEnabled = true.
///
/// Failure behavior is governed by Features:TenantDualWriteStrictMode:
///   false (default) — log error, return; originating Identity operation is unaffected.
///   true            — throw; originating Identity operation is aborted with 502.
///
/// Named HttpClient "TenantSyncInternal" must be registered in DI with:
///   BaseAddress = TenantService:InternalUrl
///   Timeout     = 5s
///   Default header X-Sync-Token = TenantService:SyncSecret
/// </summary>
public sealed class HttpTenantSyncAdapter : ITenantSyncAdapter
{
    private readonly IHttpClientFactory                 _httpClientFactory;
    private readonly bool                              _strictMode;
    private readonly ILogger<HttpTenantSyncAdapter>   _logger;

    public HttpTenantSyncAdapter(
        IHttpClientFactory              httpClientFactory,
        bool                            strictMode,
        ILogger<HttpTenantSyncAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _strictMode        = strictMode;
        _logger            = logger;
    }

    public async Task SyncAsync(IdentityTenantSyncRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[TenantDualWrite] Triggered — {EventType} for TenantId={TenantId} Code={Code}",
            request.EventType,
            request.TenantId,
            request.Code);

        try
        {
            var client = _httpClientFactory.CreateClient("TenantSyncInternal");

            var body = new
            {
                tenantId            = request.TenantId,
                code                = request.Code,
                displayName         = request.DisplayName,
                status              = request.Status,
                subdomain           = request.Subdomain,
                logoDocumentId      = request.LogoDocumentId,
                logoWhiteDocumentId = request.LogoWhiteDocumentId,
                sourceCreatedAtUtc  = request.SourceCreatedAtUtc,
                sourceUpdatedAtUtc  = request.SourceUpdatedAtUtc,
                eventType           = request.EventType,
            };

            var response = await client.PostAsJsonAsync(
                "/api/internal/tenant-sync/upsert", body, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "[TenantDualWrite] Succeeded — TenantId={TenantId} Code={Code} Status={Status}",
                    request.TenantId,
                    request.Code,
                    (int)response.StatusCode);
            }
            else
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "[TenantDualWrite] Non-success response — TenantId={TenantId} Code={Code} " +
                    "HttpStatus={Status} Body={Body}",
                    request.TenantId,
                    request.Code,
                    (int)response.StatusCode,
                    detail);

                if (_strictMode)
                    throw new InvalidOperationException(
                        $"Tenant sync returned {(int)response.StatusCode} for TenantId={request.TenantId}.");
            }
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "[TenantDualWrite] Timeout — TenantId={TenantId} Code={Code}",
                request.TenantId,
                request.Code);

            if (_strictMode)
                throw new TimeoutException(
                    $"Tenant sync timed out for TenantId={request.TenantId}.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "[TenantDualWrite] Transport error — TenantId={TenantId} Code={Code}",
                request.TenantId,
                request.Code);

            if (_strictMode)
                throw;
        }
        catch (Exception ex) when (!_strictMode)
        {
            _logger.LogError(
                ex,
                "[TenantDualWrite] Unexpected error (non-strict, continuing) — TenantId={TenantId}",
                request.TenantId);
        }
    }
}
