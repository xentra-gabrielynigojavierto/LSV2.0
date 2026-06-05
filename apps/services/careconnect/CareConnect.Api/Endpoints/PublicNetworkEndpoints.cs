using BuildingBlocks.Exceptions;
using CareConnect.Application.Cache;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuditVisibility = LegalSynq.AuditClient.Enums.VisibilityScope;

namespace CareConnect.Api.Endpoints;

// CC2-INT-B07 — Public Network Surface.
// CC2-INT-B08 — Public Referral Initiation (POST /api/public/referrals).
// These endpoints are intentionally anonymous — no JWT or platform_session required.
// Tenant isolation is enforced via the X-Tenant-Id header, which is resolved
// server-side by the Next.js BFF from the request subdomain → Tenant service lookup.
// The caller (Next.js Server Component / BFF proxy) NEVER reads this header from user input;
// it resolves the tenant from the subdomain and forwards only the GUID.
//
// BLK-SEC-02-02: Trust boundary enforced via two-layer validation:
//   Layer 1 — X-Internal-Gateway-Secret: proves request passed through the trusted YARP gateway.
//   Layer 2 — X-Tenant-Id-Sig: HMAC-SHA256 of X-Tenant-Id signed by the BFF using
//             PublicTrustBoundary:InternalRequestSecret; proves X-Tenant-Id was not spoofed.
//
// Spoofed X-Tenant-Id from direct gateway callers → rejected at Layer 2 (no valid HMAC).
// Direct-to-service requests bypassing the gateway → rejected at Layer 1 (no gateway secret).
public static class PublicNetworkEndpoints
{
    public static void MapPublicNetworkEndpoints(this WebApplication app)
    {
        // All public routes share the /api/public/network prefix.
        // The Gateway is configured to route /careconnect/api/public/** anonymously.
        var group = app.MapGroup("/api/public/network");

        // ── GET /api/public/network ─────────────────────────────────────────
        // Lists all networks for the resolved tenant.
        // Header: X-Tenant-Id (GUID, resolved from subdomain by Next.js BFF)
        group.MapGet("/", async (
            HttpContext        http,
            IConfiguration     config,
            INetworkRepository repo,
            IMemoryCache       cache,
            CancellationToken  ct) =>
        {
            var tenantId = ValidateTrustBoundaryAndResolveTenantId(http, config);
            if (tenantId == null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            // BLK-PERF-02: Cache the network list per tenant. Trust boundary
            // validation (above) has already verified tenantId is trustworthy.
            // Cache key is tenant-scoped — different tenants never share an entry.
            var summaries = await cache.GetOrCreateAsync(
                CareConnectCacheKeys.PublicNetworkList(tenantId.Value),
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CareConnectCacheTtl.PublicNetwork;
                    entry.Size = 1;
                    // BLK-PERF-01: Single query — replaces the N+1 loop of
                    // GetAllByTenantAsync + N×GetWithProvidersAsync.
                    var rows = await repo.GetAllWithProviderCountAsync(tenantId.Value, ct);
                    return rows
                        .Select(r => new PublicNetworkSummary(r.Id, r.Name, r.Description ?? string.Empty, r.ProviderCount))
                        .ToList();
                });

            return Results.Ok(summaries);
        }).AllowAnonymous();

        // ── GET /api/public/network/{id}/providers ──────────────────────────
        group.MapGet("/{id:guid}/providers", async (
            Guid               id,
            HttpContext         http,
            IConfiguration      config,
            INetworkRepository  repo,
            IMemoryCache        cache,
            CancellationToken   ct) =>
        {
            var tenantId = ValidateTrustBoundaryAndResolveTenantId(http, config);
            if (tenantId == null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            // BLK-PERF-02: Cache provider list per tenant+network for 60 s.
            // Invalidated on network write (PUT/DELETE network, POST/DELETE provider).
            var items = await cache.GetOrCreateAsync(
                CareConnectCacheKeys.PublicNetworkProviders(tenantId.Value, id),
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CareConnectCacheTtl.PublicNetwork;
                    entry.Size = 1;

                    var network = await repo.GetByIdAsync(tenantId.Value, id, ct);
                    if (network == null) return null;

                    var providers = await repo.GetNetworkProvidersAsync(tenantId.Value, id, ct);
                    return providers
                        .Select(p => new PublicProviderItem(
                            p.Id, p.Name, p.OrganizationName,
                            p.Phone, p.City, p.State, p.PostalCode,
                            p.IsActive, p.AcceptingReferrals, p.AccessStage, null))
                        .ToList();
                });

            return items == null ? Results.NotFound() : Results.Ok(items);
        }).AllowAnonymous();

        // ── GET /api/public/network/{id}/providers/markers ──────────────────
        group.MapGet("/{id:guid}/providers/markers", async (
            Guid               id,
            HttpContext         http,
            IConfiguration      config,
            INetworkRepository  repo,
            IMemoryCache        cache,
            CancellationToken   ct) =>
        {
            var tenantId = ValidateTrustBoundaryAndResolveTenantId(http, config);
            if (tenantId == null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            // BLK-PERF-02: Cache map markers per tenant+network for 60 s.
            // Invalidated on network write (PUT/DELETE network, POST/DELETE provider).
            var markers = await cache.GetOrCreateAsync(
                CareConnectCacheKeys.PublicNetworkMarkers(tenantId.Value, id),
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CareConnectCacheTtl.PublicNetwork;
                    entry.Size = 1;

                    var network = await repo.GetByIdAsync(tenantId.Value, id, ct);
                    if (network == null) return null;

                    var providers = await repo.GetNetworkProvidersAsync(tenantId.Value, id, ct);
                    // Include every provider so the client can geocode those
                    // whose coordinates have not yet been stored (0.0 signals
                    // "needs geocoding" to the client-side geocoder).
                    return providers
                        .Select(p => new PublicProviderMarker(
                            p.Id, p.Name, p.OrganizationName,
                            p.City, p.State, p.AcceptingReferrals,
                            p.Latitude ?? 0.0, p.Longitude ?? 0.0))
                        .ToList();
                });

            return markers == null ? Results.NotFound() : Results.Ok(markers);
        }).AllowAnonymous();

        // ── GET /api/public/network/{id}/detail ────────────────────────────
        group.MapGet("/{id:guid}/detail", async (
            Guid               id,
            HttpContext         http,
            IConfiguration      config,
            INetworkRepository  repo,
            IMemoryCache        cache,
            CancellationToken   ct) =>
        {
            var tenantId = ValidateTrustBoundaryAndResolveTenantId(http, config);
            if (tenantId == null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            // BLK-PERF-02: Cache the full detail payload (providers + markers) per
            // tenant+network for 60 s. Single factory covers both data sets to avoid
            // a split-brain cache state between the /providers and /detail endpoints.
            var detail = await cache.GetOrCreateAsync(
                CareConnectCacheKeys.PublicNetworkDetail(tenantId.Value, id),
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CareConnectCacheTtl.PublicNetwork;
                    entry.Size = 1;

                    var network = await repo.GetWithProvidersAsync(tenantId.Value, id, ct);
                    if (network == null) return null;

                    // BLK-PERF-01: Providers already loaded via Include — no extra round-trip.
                    var providers = network.NetworkProviders
                        .Where(np => np.Provider != null)
                        .Select(np => np.Provider!)
                        .OrderBy(p => p.Name)
                        .ToList();

                    var items = providers
                        .Select(p => new PublicProviderItem(
                            p.Id, p.Name, p.OrganizationName,
                            p.Phone, p.City, p.State, p.PostalCode,
                            p.IsActive, p.AcceptingReferrals, p.AccessStage, null))
                        .ToList();

                    // Include every provider (0.0 lat/lng = needs client-side geocoding).
                    var markers = providers
                        .Select(p => new PublicProviderMarker(
                            p.Id, p.Name, p.OrganizationName,
                            p.City, p.State, p.AcceptingReferrals,
                            p.Latitude ?? 0.0, p.Longitude ?? 0.0))
                        .ToList();

                    return new PublicNetworkDetail(network.Id, network.Name, network.Description, items, markers);
                });

            return detail == null ? Results.NotFound() : Results.Ok(detail);
        }).AllowAnonymous();

        // ── GET /api/public/treatment-types ────────────────────────────────
        // Returns the platform-wide treatment type lookup list.
        // Trust-boundary validated (same as other public endpoints).
        app.MapGet("/api/public/treatment-types", async (
            HttpContext       http,
            IConfiguration    config,
            CareConnect.Infrastructure.Data.CareConnectDbContext db,
            CancellationToken ct) =>
        {
            var tenantId = ValidateTrustBoundaryAndResolveTenantId(http, config);
            if (tenantId == null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            try
            {
                var conn = db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync(ct);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    SELECT `Id`, `Name`, `Category`, `DisplayOrder`
                    FROM `cc_TreatmentTypes`
                    WHERE `IsActive` = 1
                    ORDER BY `DisplayOrder` ASC, `Name` ASC
                    """;
                var result = new List<object>();
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    result.Add(new
                    {
                        id           = reader.GetString(0),
                        name         = reader.GetString(1),
                        category     = reader.IsDBNull(2) ? null : reader.GetString(2),
                        displayOrder = reader.GetInt32(3),
                    });
                }
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                var log = http.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("CareConnect.PublicTreatmentTypes");
                log.LogError(ex, "Failed to query cc_TreatmentTypes.");
                return Results.Problem("Unable to load treatment types.");
            }
        }).AllowAnonymous();

        // ── POST /api/public/referrals ──────────────────────────────────────
        // CC2-INT-B08 — Public referral initiation.
        // Accepts an unauthenticated referral submission from the public network directory.
        // Rate-limited (10 req/min per IP, policy registered in Program.cs) to prevent abuse.
        // Tenant isolation: X-Tenant-Id set server-side by Next.js BFF from subdomain — never
        // read from user input.
        // Token/notification flow: delegated to IReferralService.CreateAsync, which fires:
        //   - SendNewReferralNotificationAsync  (email + signed token for URL-stage providers)
        //   - SendProviderAssignedNotificationAsync (platform Notifications → portal visibility)
        app.MapPost("/api/public/referrals", async (
            PublicReferralRequest req,
            HttpContext           http,
            IConfiguration       config,
            IProviderRepository  providerRepo,
            IReferralService     referralSvc,
            ILoggerFactory       loggerFactory,
            CancellationToken    ct) =>
        {
            var logger = loggerFactory.CreateLogger("CareConnect.PublicReferrals");
            return await HandlePublicReferral(req, http, config, providerRepo, referralSvc, logger, ct);
        })
        .AllowAnonymous()
        .RequireRateLimiting("public-referral-limit");

        // ── POST /api/public/referrals/{referralId}/attachments/upload ──────────
        // CC2-INT-B08 — Public document upload for referrals submitted from the public network form.
        // Secured by the same two-layer trust boundary as the referral creation endpoint.
        // Accepts a single file (multipart/form-data) and proxies it to the Documents service.
        app.MapPost("/api/public/referrals/{referralId:guid}/attachments/upload", async (
            Guid                                   referralId,
            HttpRequest                            httpRequest,
            HttpContext                            http,
            IConfiguration                         config,
            IReferralAttachmentService             attachmentSvc,
            Microsoft.Extensions.Options.IOptions<CareConnect.Api.Options.AttachmentUploadOptions> uploadOptions,
            ILoggerFactory                         loggerFactory,
            CancellationToken                      ct) =>
        {
            var logger = loggerFactory.CreateLogger("CareConnect.PublicReferrals");

            var tenantId = ValidateTrustBoundaryAndResolveTenantId(http, config);
            if (tenantId == null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            if (!httpRequest.HasFormContentType)
                return Results.BadRequest(new { error = "Request must be multipart/form-data." });

            var form = await httpRequest.ReadFormAsync(ct);
            if (form.Files.Count == 0)
                return Results.BadRequest(new { error = "No file was provided." });

            var file    = form.Files[0];
            var options = uploadOptions.Value;

            if (file.Length > options.MaxFileSizeBytes)
                return Results.BadRequest(new { error = $"File size exceeds the maximum allowed size of {options.MaxFileSizeBytes / (1024 * 1024)} MB." });

            var normalizedType = file.ContentType?.Split(';')[0].Trim().ToLowerInvariant() ?? string.Empty;
            if (!options.AllowedContentTypes.Contains(normalizedType, StringComparer.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = $"File type '{file.ContentType}' is not permitted.", allowed = options.AllowedContentTypes });

            try
            {
                await using var stream       = file.OpenReadStream();
                var             uploadReq    = new UploadAttachmentRequest { Scope = AttachmentScope.Shared };
                var             result       = await attachmentSvc.UploadAsync(
                    tenantId.Value,
                    referralId,
                    userId: null,
                    stream,
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    uploadReq,
                    ct);

                logger.LogInformation(
                    "Public referral document uploaded: ReferralId={ReferralId} AttachmentId={AttachmentId} File={FileName}",
                    referralId, result.Id, file.FileName);

                return Results.Created($"/api/public/referrals/{referralId}/attachments/{result.Id}", result);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Public referral document upload failed for referral {ReferralId}.", referralId);
                return Results.Problem("An unexpected error occurred while uploading the document.");
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting("public-referral-limit")
        .DisableAntiforgery();

        // ── GET /api/public/referrer-status?email=xxx ────────────────────────
        // CC-PORTAL-CHECK — After a public referral is submitted, the success screen
        // checks whether the law firm's email is already registered with an active
        // CareConnect portal account.
        //
        // Response: { hasPortalAccess: bool }
        //   true  → show "Login to CareConnect to view your referrals"
        //   false → show "Activate your free account" (default enrollment CTA)
        //
        // Delegates the user-record lookup to the Identity service via
        // IIdentityOrganizationService.CheckReferrerPortalAccessAsync.
        // Any infrastructure failure → hasPortalAccess = false (safe default).
        app.MapGet("/api/public/referrer-status", async (
            string?                    email,
            HttpContext                 http,
            IConfiguration             config,
            IIdentityOrganizationService identityOrgs,
            CancellationToken          ct) =>
        {
            var tenantId = ValidateTrustBoundaryAndResolveTenantId(http, config);
            if (tenantId is null)
                return Results.StatusCode(403);

            if (string.IsNullOrWhiteSpace(email))
                return Results.Ok(new { hasPortalAccess = false });

            var hasAccess = await identityOrgs.CheckReferrerPortalAccessAsync(email.Trim(), ct);
            return Results.Ok(new { hasPortalAccess = hasAccess });
        })
        .AllowAnonymous();
    }

    private static async Task<IResult> HandlePublicReferral(
        PublicReferralRequest req,
        HttpContext           http,
        IConfiguration       config,
        IProviderRepository  providerRepo,
        IReferralService     referralSvc,
        ILogger              logger,
        CancellationToken    ct)
    {
            var tenantId = ValidateTrustBoundaryAndResolveTenantId(http, config);
            if (tenantId == null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            // Input validation
            var errors = ValidatePublicReferralRequest(req);
            if (errors.Count > 0)
                return Results.UnprocessableEntity(new { message = "Validation failed.", errors });

            // Provider lookup + AcceptingReferrals guard.
            // Cross-tenant lookup because providers are a platform-wide marketplace.
            Provider? provider;
            try { provider = await providerRepo.GetByIdCrossAsync(req.ProviderId, ct); }
            catch { provider = null; }

            if (provider is null)
                return Results.NotFound(new { message = "Provider not found." });

            if (!provider.AcceptingReferrals)
                return Results.UnprocessableEntity(new
                {
                    message = "This provider is not currently accepting referrals."
                });

            // Map to the internal CreateReferralRequest.
            // ReferrerName/ReferrerEmail drive the signed-token email notification flow.
            // Assemble structured notes: user-supplied notes + address + dates of accident.
            var notesParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(req.Notes))
                notesParts.Add(req.Notes.Trim());
            if (!string.IsNullOrWhiteSpace(req.PatientAddress))
                notesParts.Add($"Patient Address: {req.PatientAddress.Trim()}");
            if (req.PatientDateOfAccident.HasValue)
                notesParts.Add($"Date of Accident: {req.PatientDateOfAccident.Value:yyyy-MM-dd}");

            var createReq = new CreateReferralRequest
            {
                ProviderId              = req.ProviderId,
                ClientFirstName         = req.PatientFirstName.Trim(),
                ClientLastName          = req.PatientLastName.Trim(),
                ClientPhone             = req.PatientPhone.Trim(),
                ClientEmail             = req.PatientEmail?.Trim() ?? string.Empty,
                ClientDob               = req.PatientDateOfBirth.HasValue
                                            ? req.PatientDateOfBirth.Value.ToDateTime(TimeOnly.MinValue)
                                            : null,
                RequestedService        = string.IsNullOrWhiteSpace(req.ServiceType)
                                            ? "General Referral"
                                            : req.ServiceType.Trim(),
                Urgency                 = Referral.ValidUrgencies.Normal,
                Notes                   = notesParts.Count > 0 ? string.Join("\n", notesParts) : null,
                ReferrerName            = req.SenderName.Trim(),
                ReferrerEmail           = req.SenderEmail.Trim(),
                ReferringOrganizationId = null,   // public — no org context
                ReceivingOrganizationId = null,
            };

            // Create referral via the existing pipeline.
            // CreateAsync persists the referral and fires fire-and-observe notifications.
            // userId = null (anonymous submission).
            try
            {
                var referral = await referralSvc.CreateAsync(tenantId.Value, userId: null, createReq, ct);

                logger.LogInformation(
                    "Public referral submitted: ReferralId={ReferralId} ProviderId={ProviderId} " +
                    "Stage={Stage} Tenant={TenantId}",
                    referral.Id, req.ProviderId, provider.AccessStage, tenantId.Value);

                var response = new PublicReferralResponse(
                    referral.Id,
                    provider.Id,
                    provider.Name,
                    provider.AccessStage,
                    "Referral submitted successfully. The provider will be in touch shortly.");

                return Results.Created($"/api/public/referrals/{referral.Id}", response);
            }
            catch (NotFoundException ex)
            {
                logger.LogWarning(ex, "Public referral: provider not found mid-creation.");
                return Results.NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Public referral creation failed for provider {ProviderId}.", req.ProviderId);
                return Results.Problem("An unexpected error occurred while submitting your referral.");
            }
    }

    // ── Trust Boundary Validation (BLK-SEC-02-02) ─────────────────────────

    /// <summary>
    /// Validates the two-layer public trust boundary and returns the resolved TenantId.
    ///
    /// Layer 1 — X-Internal-Gateway-Secret:
    ///   Proves the request was forwarded by the trusted YARP gateway.
    ///   The gateway strips any client-supplied value and injects its own configured secret.
    ///   A direct caller bypassing the gateway cannot supply the correct value.
    ///
    /// Layer 2 — X-Tenant-Id-Sig (HMAC-SHA256):
    ///   Proves X-Tenant-Id was signed by the trusted Next.js BFF.
    ///   HMAC-SHA256(X-Tenant-Id, InternalRequestSecret) computed server-side by the BFF.
    ///   A caller going through the gateway but supplying an arbitrary X-Tenant-Id
    ///   cannot forge the signature without knowing the shared secret.
    ///
    /// Returns null and logs a warning if validation fails.
    /// The fallback path (validation disabled / secret not configured) is intentionally
    /// limited to environments where the secret is not set — logged as a warning.
    /// </summary>
    private static Guid? ValidateTrustBoundaryAndResolveTenantId(
        HttpContext    http,
        IConfiguration config)
    {
        var logger = http.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("CareConnect.PublicTrustBoundary");

        var secret = config["PublicTrustBoundary:InternalRequestSecret"];

        // BLK-OBS-01: resolve correlation/request ID for all security-event log entries.
        var requestId = http.Items["CorrelationId"]?.ToString() ?? http.TraceIdentifier;

        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogWarning(
                "PublicTrustBoundary:InternalRequestSecret is not configured — " +
                "trust boundary validation is DISABLED. Set this value in all non-dev environments. " +
                "Path={Path} RequestId={RequestId}", http.Request.Path, requestId);
            return ResolveTenantIdRaw(http);
        }

        // Layer 1: validate gateway origin marker
        var gatewaySecret = http.Request.Headers["X-Internal-Gateway-Secret"].FirstOrDefault();
        if (gatewaySecret != secret)
        {
            logger.LogWarning(
                "Public request rejected: X-Internal-Gateway-Secret mismatch (Layer 1). " +
                "RemoteIp={RemoteIp} Path={Path} RequestId={RequestId}",
                http.Connection.RemoteIpAddress, http.Request.Path, requestId);
            EmitTrustBoundaryRejectedAudit(http, "layer1-gateway-secret-mismatch", requestId);
            return null;
        }

        // Layer 2: validate HMAC signature of X-Tenant-Id
        var tenantIdRaw = http.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var sig         = http.Request.Headers["X-Tenant-Id-Sig"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(tenantIdRaw))
        {
            logger.LogWarning(
                "Public request rejected: X-Tenant-Id header missing (Layer 2). " +
                "RemoteIp={RemoteIp} Path={Path} RequestId={RequestId}",
                http.Connection.RemoteIpAddress, http.Request.Path, requestId);
            EmitTrustBoundaryRejectedAudit(http, "layer2-tenant-id-missing", requestId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(sig))
        {
            logger.LogWarning(
                "Public request rejected: X-Tenant-Id-Sig header missing (Layer 2). " +
                "RemoteIp={RemoteIp} Path={Path} RequestId={RequestId}",
                http.Connection.RemoteIpAddress, http.Request.Path, requestId);
            EmitTrustBoundaryRejectedAudit(http, "layer2-tenant-id-sig-missing", requestId);
            return null;
        }

        if (!TryValidateHmac(tenantIdRaw, sig, secret))
        {
            logger.LogWarning(
                "Public request rejected: X-Tenant-Id-Sig HMAC validation failed (Layer 2). " +
                "RemoteIp={RemoteIp} Path={Path} RequestId={RequestId}",
                http.Connection.RemoteIpAddress, http.Request.Path, requestId);
            EmitTrustBoundaryRejectedAudit(http, "layer2-hmac-validation-failed", requestId);
            return null;
        }

        if (!Guid.TryParse(tenantIdRaw, out var tenantId))
        {
            logger.LogWarning(
                "Public request rejected: X-Tenant-Id is not a valid GUID. " +
                "RemoteIp={RemoteIp} Path={Path} RequestId={RequestId}",
                http.Connection.RemoteIpAddress, http.Request.Path, requestId);
            EmitTrustBoundaryRejectedAudit(http, "layer2-tenant-id-invalid-guid", requestId);
            return null;
        }

        return tenantId;
    }

    /// <summary>
    /// Validates HMAC-SHA256(data, secret) against the provided base64-encoded signature.
    /// Uses constant-time comparison to prevent timing side-channel attacks.
    /// </summary>
    private static bool TryValidateHmac(string data, string sig, string secret)
    {
        try
        {
            byte[] sigBytes;
            try { sigBytes = Convert.FromBase64String(sig); }
            catch { return false; }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));

            if (sigBytes.Length != expected.Length) return false;
            return CryptographicOperations.FixedTimeEquals(expected, sigBytes);
        }
        catch
        {
            return false;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Raw tenant ID extraction — used only when trust boundary validation is disabled
    /// (unconfigured secret, typically in local dev without the full gateway stack).
    /// </summary>
    private static Guid? ResolveTenantIdRaw(HttpContext http)
    {
        var raw = http.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>
    /// CC2-INT-B08: Validates a public referral submission.
    /// Returns a dictionary of field → error message for any validation failures.
    /// </summary>
    private static Dictionary<string, string> ValidatePublicReferralRequest(PublicReferralRequest req)
    {
        var errors = new Dictionary<string, string>();

        if (req.ProviderId == Guid.Empty)
            errors["providerId"] = "A valid provider ID is required.";

        if (string.IsNullOrWhiteSpace(req.SenderName) || req.SenderName.Trim().Length < 2)
            errors["senderName"] = "Your name is required (minimum 2 characters).";
        else if (req.SenderName.Length > 200)
            errors["senderName"] = "Name must not exceed 200 characters.";

        if (string.IsNullOrWhiteSpace(req.SenderEmail))
            errors["senderEmail"] = "Your email address is required.";
        else if (!IsValidEmail(req.SenderEmail))
            errors["senderEmail"] = "A valid email address is required.";

        if (string.IsNullOrWhiteSpace(req.PatientFirstName) || req.PatientFirstName.Trim().Length < 1)
            errors["patientFirstName"] = "Patient first name is required.";
        else if (req.PatientFirstName.Length > 100)
            errors["patientFirstName"] = "First name must not exceed 100 characters.";

        if (string.IsNullOrWhiteSpace(req.PatientLastName) || req.PatientLastName.Trim().Length < 1)
            errors["patientLastName"] = "Patient last name is required.";
        else if (req.PatientLastName.Length > 100)
            errors["patientLastName"] = "Last name must not exceed 100 characters.";

        if (string.IsNullOrWhiteSpace(req.PatientPhone))
            errors["patientPhone"] = "Patient phone number is required.";
        else if (req.PatientPhone.Trim().Length < 7 || req.PatientPhone.Length > 30)
            errors["patientPhone"] = "Please enter a valid phone number.";

        if (!string.IsNullOrWhiteSpace(req.PatientEmail) && !IsValidEmail(req.PatientEmail))
            errors["patientEmail"] = "Please enter a valid patient email address.";

        if (!req.PatientDateOfBirth.HasValue)
            errors["patientDateOfBirth"] = "Patient date of birth is required.";
        else if (req.PatientDateOfBirth.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            errors["patientDateOfBirth"] = "Date of birth cannot be in the future.";

        if (!req.PatientDateOfAccident.HasValue)
            errors["patientDateOfAccident"] = "Date of accident is required.";
        else if (req.PatientDateOfAccident.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            errors["patientDateOfAccident"] = "Date of accident cannot be in the future.";

        if (req.PatientAddress is not null && req.PatientAddress.Length > 500)
            errors["patientAddress"] = "Address must not exceed 500 characters.";

        if (req.ServiceType is not null && req.ServiceType.Length > 200)
            errors["serviceType"] = "Service type must not exceed 200 characters.";

        if (req.Notes is not null && req.Notes.Length > 2000)
            errors["notes"] = "Notes must not exceed 2000 characters.";

        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        try { _ = new MailAddress(email.Trim()); return true; }
        catch { return false; }
    }

    // ── BLK-COMP-01: Trust boundary rejection audit ───────────────────────────
    // Emits security.trust_boundary.rejected to the Audit Service for every failed
    // validation so that direct-service probes and header spoofing attempts are
    // permanently reconstructable under SOC 2 / HIPAA audit.
    //
    // Fire-and-observe: caller does NOT await — this never gates the 403 response
    // on audit delivery success ("persist-first, audit-second" rule).
    //
    // Resolution is optional via GetService<IAuditEventClient>() so the helper is
    // safe to call even in environments that have not registered the audit client
    // (e.g. unit-test hosts).
    private static void EmitTrustBoundaryRejectedAudit(
        HttpContext http,
        string      reason,
        string      requestId)
    {
        var auditClient = http.RequestServices.GetService<IAuditEventClient>();
        if (auditClient is null) return;

        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "security.trust_boundary.rejected",
            EventCategory = EventCategory.Security,
            SourceSystem  = "care-connect",
            SourceService = "public-network",
            Visibility    = AuditVisibility.Platform,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Action        = "TrustBoundaryRejected",
            Description   = $"Public request rejected at trust boundary: {reason}.",
            Outcome       = "denied",
            Actor = new AuditEventActorDto
            {
                Type      = ActorType.Anonymous,
                IpAddress = http.Connection.RemoteIpAddress?.ToString(),
            },
            Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Service },
            CorrelationId = requestId,
            Metadata      = JsonSerializer.Serialize(new
            {
                reason = reason,
                path   = http.Request.Path.Value,
            }),
            Tags = ["security", "trust-boundary", "rejection"],
        });
    }
}
