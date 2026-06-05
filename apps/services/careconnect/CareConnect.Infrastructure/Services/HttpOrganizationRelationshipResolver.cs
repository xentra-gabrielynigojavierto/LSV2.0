using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CareConnect.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareConnect.Infrastructure.Services;

/// <summary>
/// Live HTTP implementation of IOrganizationRelationshipResolver.
///
/// Queries the Identity service admin endpoint to find an active
/// OrganizationRelationship between two organizations:
///
///   GET {BaseUrl}/api/admin/organization-relationships
///         ?sourceOrgId={referringOrgId}&amp;activeOnly=true&amp;pageSize=200
///
/// Resolution logic:
///   1. If BaseUrl is not configured → return null (disabled mode, warned at startup).
///   2. Send the request with the configured timeout, optionally with a service auth header.
///   3. Deserialize the paged response.
///   4. Return the first item whose targetOrganizationId matches receivingOrganizationId.
///   5. On any failure (timeout, 4xx/5xx, network error, parse error) → return null.
///      Referral creation is never blocked by relationship resolution.
///
/// Configured via IdentityServiceOptions (appsettings: "IdentityService").
/// </summary>
public sealed class HttpOrganizationRelationshipResolver : IOrganizationRelationshipResolver
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IdentityServiceOptions _options;
    private readonly ILogger<HttpOrganizationRelationshipResolver> _logger;

    /// <summary>
    /// Pre-computed at construction time so the "disabled" condition is not re-evaluated
    /// on every referral creation call, and the warning is logged once per DI scope.
    /// </summary>
    private readonly bool _isEnabled;

    public HttpOrganizationRelationshipResolver(
        IHttpClientFactory httpClientFactory,
        IOptions<IdentityServiceOptions> options,
        ILogger<HttpOrganizationRelationshipResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _isEnabled = !string.IsNullOrWhiteSpace(_options.BaseUrl);

        if (!_isEnabled)
        {
            _logger.LogWarning(
                "IdentityService:BaseUrl is not configured. " +
                "Organization relationship resolution is disabled — " +
                "OrganizationRelationshipId will always be null on new referrals. " +
                "Set IdentityService__BaseUrl to enable live cross-service resolution.");
        }
    }

    public async Task<Guid?> FindActiveRelationshipAsync(
        Guid referringOrganizationId,
        Guid receivingOrganizationId,
        CancellationToken ct = default)
    {
        if (!_isEnabled)
            return null;

        try
        {
            using var client = _httpClientFactory.CreateClient("IdentityService");
            client.BaseAddress = new Uri(_options.BaseUrl!.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

            // Apply optional service-to-service auth header (e.g. API key, internal token)
            if (!string.IsNullOrWhiteSpace(_options.AuthHeaderName) &&
                !string.IsNullOrWhiteSpace(_options.AuthHeaderValue))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    _options.AuthHeaderName, _options.AuthHeaderValue);
            }

            var url = $"api/admin/organization-relationships" +
                      $"?sourceOrgId={referringOrganizationId:D}" +
                      $"&activeOnly=true" +
                      $"&pageSize=200";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var response = await client.GetAsync(url, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Identity relationship lookup returned HTTP {StatusCode} for " +
                    "source={ReferringOrgId}. Proceeding with null relationship.",
                    (int)response.StatusCode, referringOrganizationId);
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<OrgRelationshipPagedResponse>(
                cancellationToken: cts.Token);

            if (body?.Items is null || body.Items.Count == 0)
            {
                _logger.LogDebug(
                    "No active relationships found for source={ReferringOrgId}.",
                    referringOrganizationId);
                return null;
            }

            var match = body.Items.FirstOrDefault(item =>
                item.IsActive &&
                item.TargetOrganizationId == receivingOrganizationId);

            if (match is null)
            {
                _logger.LogDebug(
                    "No active relationship matched between " +
                    "source={ReferringOrgId} and target={ReceivingOrgId} " +
                    "(checked {Count} candidate(s)).",
                    referringOrganizationId, receivingOrganizationId, body.Items.Count);
                return null;
            }

            _logger.LogDebug(
                "Resolved OrganizationRelationshipId={RelationshipId} " +
                "for source={ReferringOrgId} → target={ReceivingOrgId}.",
                match.Id, referringOrganizationId, receivingOrganizationId);

            return match.Id;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Identity relationship lookup timed out after {TimeoutSeconds}s " +
                "for source={ReferringOrgId}. Proceeding with null relationship.",
                _options.TimeoutSeconds, referringOrganizationId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Identity relationship lookup failed for source={ReferringOrgId}. " +
                "Proceeding with null relationship.",
                referringOrganizationId);
            return null;
        }
    }

    // ── Private response models (scoped to this class — not shared DTOs) ──────

    private sealed class OrgRelationshipPagedResponse
    {
        [JsonPropertyName("items")]
        public List<OrgRelationshipItem>? Items { get; set; }

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }
    }

    private sealed class OrgRelationshipItem
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("sourceOrganizationId")]
        public Guid SourceOrganizationId { get; set; }

        [JsonPropertyName("targetOrganizationId")]
        public Guid TargetOrganizationId { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }
    }
}
