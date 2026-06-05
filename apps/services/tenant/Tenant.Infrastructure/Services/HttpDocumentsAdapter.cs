using Microsoft.Extensions.Logging;
using Tenant.Application.Interfaces;

namespace Tenant.Infrastructure.Services;

/// <summary>
/// TENANT-B10: HTTP adapter for Documents service logo-registration calls.
///
/// All operations are non-fatal: a failure is logged as a warning and does NOT
/// roll back the preceding Tenant DB write.  This mirrors the behaviour of the
/// equivalent helper in Identity (RegisterLogoInDocumentsAsync).
///
/// Named HTTP client: "DocumentsInternal" — configured in DependencyInjection.cs
/// with base address from <c>DocumentsService:InternalUrl</c>.
/// </summary>
public class HttpDocumentsAdapter : IDocumentsAdapter
{
    private readonly IHttpClientFactory           _httpClientFactory;
    private readonly ILogger<HttpDocumentsAdapter> _logger;

    public HttpDocumentsAdapter(
        IHttpClientFactory            httpClientFactory,
        ILogger<HttpDocumentsAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    /// <inheritdoc/>
    public async Task RegisterLogoAsync(
        Guid              documentId,
        Guid              tenantId,
        string?           authHeader,
        CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentsInternal");

            using var req = new HttpRequestMessage(
                HttpMethod.Put,
                $"/documents/{documentId}/logo-registration");

            if (!string.IsNullOrEmpty(authHeader))
                req.Headers.TryAddWithoutValidation("Authorization", authHeader);

            req.Headers.TryAddWithoutValidation("X-Admin-Target-Tenant", tenantId.ToString());

            var res = await client.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "[Tenant-B10] RegisterLogoAsync: Documents returned {Status} for document {DocId} / tenant {TenantId}. Body: {Body}",
                    (int)res.StatusCode, documentId, tenantId, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "[Tenant-B10] RegisterLogoAsync: non-fatal error for document {DocId} / tenant {TenantId}: {Message}",
                documentId, tenantId, ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task DeregisterLogoAsync(
        Guid              tenantId,
        string?           authHeader,
        CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentsInternal");

            using var req = new HttpRequestMessage(
                HttpMethod.Delete,
                "/documents/logo-registration");

            if (!string.IsNullOrEmpty(authHeader))
                req.Headers.TryAddWithoutValidation("Authorization", authHeader);

            req.Headers.TryAddWithoutValidation("X-Admin-Target-Tenant", tenantId.ToString());

            var res = await client.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "[Tenant-B10] DeregisterLogoAsync: Documents returned {Status} for tenant {TenantId}. Body: {Body}",
                    (int)res.StatusCode, tenantId, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "[Tenant-B10] DeregisterLogoAsync: non-fatal error for tenant {TenantId}: {Message}",
                tenantId, ex.Message);
        }
    }
}
