using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using System.Security.Cryptography;
using System.Text;
using AuditVisibility = LegalSynq.AuditClient.Enums.VisibilityScope;

namespace CareConnect.Api.Endpoints;

// CC2-ENROLL — Provider / law-firm self-enrollment endpoints.
//
// All endpoints live under /api/public/enrollment and are intentionally anonymous —
// no JWT or platform_session required.
//
// Trust boundary is enforced via the same two-layer validation used by
// CC2-INT-B07 (PublicNetworkEndpoints):
//   Layer 1 — X-Internal-Gateway-Secret: proves YARP gateway origin.
//   Layer 2 — X-Tenant-Id-Sig HMAC: proves BFF resolved the tenant (not user-supplied).
//
// The tenant resolved from the request is used for OTP notifications.
// Identity operations (org creation, user registration) use the provider's own TenantId.

public static class EnrollmentEndpoints
{
    public static void MapEnrollmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/public/enrollment");

        // GET /api/public/enrollment/prefill/{providerId}
        // Returns provider contact info for pre-filling the enrollment form.
        group.MapGet("/prefill/{providerId:guid}", async (
            Guid               providerId,
            HttpContext        http,
            IConfiguration     config,
            IProviderRepository providers,
            CancellationToken  ct) =>
        {
            var tenantId = ValidateTrustBoundary(http, config);
            if (tenantId is null)
                return Results.StatusCode(403);

            var provider = await providers.GetByIdCrossAsync(providerId, ct);
            if (provider is null)
                return Results.NotFound(new { message = "Provider not found." });

            // Providers already in COMMON_PORTAL or TENANT stage are already enrolled.
            if (ProviderAccessStage.IsAtLeast(provider.AccessStage, ProviderAccessStage.CommonPortal))
                return Results.Conflict(new { message = "This provider has already activated portal access.", alreadyEnrolled = true });

            return Results.Ok(new EnrollmentPrefillResponse(
                ProviderId:   provider.Id,
                CompanyName:  provider.OrganizationName ?? provider.Name,
                CompanyType:  "Provider",
                Email:        provider.Email,
                Phone:        provider.Phone,
                AddressLine1: provider.AddressLine1,
                City:         provider.City,
                State:        provider.State,
                PostalCode:   provider.PostalCode));
        });

        // POST /api/public/enrollment/send-otp
        // Generates a 6-digit OTP and emails it to the specified address.
        // Only intended to be called when the user changes the email from what is on record.
        group.MapPost("/send-otp", async (
            HttpContext          http,
            IConfiguration       config,
            SendOtpRequest       body,
            EnrollmentOtpStore   otpStore,
            INotificationsProducer notifProducer,
            IProviderRepository  providers,
            ILoggerFactory       loggerFactory,
            CancellationToken    ct) =>
        {
            var logger = loggerFactory.CreateLogger("CC2-ENROLL.SendOtp");

            var tenantId = ValidateTrustBoundary(http, config);
            if (tenantId is null)
                return Results.StatusCode(403);

            if (string.IsNullOrWhiteSpace(body.Email) || !IsValidEmail(body.Email))
                return Results.BadRequest(new { message = "A valid email address is required." });

            // Load provider to use its tenantId for the notification
            var notifTenantId = tenantId.Value;
            if (body.ProviderId.HasValue)
            {
                var provider = await providers.GetByIdCrossAsync(body.ProviderId.Value, ct);
                if (provider is not null) notifTenantId = provider.TenantId;
            }

            var code = otpStore.Generate(body.Email);

            var html = BuildOtpEmailHtml(body.Email, code);

            try
            {
                await notifProducer.SubmitAsync(
                    tenantId:       notifTenantId,
                    eventKey:       "enrollment.otp",
                    toAddress:      body.Email,
                    subject:        "Your CareConnect Verification Code",
                    htmlBody:       html,
                    idempotencyKey: null,
                    correlationId:  body.ProviderId?.ToString(),
                    ct:             ct);

                logger.LogInformation("CC2-ENROLL OTP sent to {Email}.", body.Email);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "CC2-ENROLL OTP email failed for {Email}. Code still stored.", body.Email);
                return Results.Problem("Failed to send verification code. Please try again.");
            }

            return Results.Ok(new { sent = true });
        });

        // POST /api/public/enrollment/register
        // Completes the self-enrollment: verifies OTP (if email changed), updates provider
        // record, creates Identity org + active user, marks provider as COMMON_PORTAL.
        group.MapPost("/register", async (
            HttpContext               http,
            IConfiguration           config,
            EnrollmentRegisterRequest body,
            EnrollmentOtpStore        otpStore,
            IIdentityOrganizationService identityOrgs,
            IProviderService          providerService,
            IProviderRepository       providers,
            IAuditEventClient         auditClient,
            ILoggerFactory            loggerFactory,
            CancellationToken         ct) =>
        {
            var logger = loggerFactory.CreateLogger("CC2-ENROLL.Register");

            var tenantId = ValidateTrustBoundary(http, config);
            if (tenantId is null)
                return Results.StatusCode(403);

            // ── Input validation ─────────────────────────────────────────────

            if (body.ProviderId == Guid.Empty)
                return Results.BadRequest(new { message = "providerId is required." });
            if (string.IsNullOrWhiteSpace(body.Email) || !IsValidEmail(body.Email))
                return Results.BadRequest(new { message = "A valid email address is required." });
            if (string.IsNullOrWhiteSpace(body.Password) || body.Password.Length < 8)
                return Results.BadRequest(new { message = "Password must be at least 8 characters." });
            if (string.IsNullOrWhiteSpace(body.FirstName))
                return Results.BadRequest(new { message = "First name is required." });

            // ── Load provider ────────────────────────────────────────────────

            var provider = await providers.GetByIdCrossAsync(body.ProviderId, ct);
            if (provider is null)
                return Results.NotFound(new { message = "Provider not found." });

            if (ProviderAccessStage.IsAtLeast(provider.AccessStage, ProviderAccessStage.CommonPortal))
                return Results.Conflict(new { message = "This provider has already activated portal access.", alreadyEnrolled = true });

            // ── OTP verification (required only when email differs from record) ──

            var emailChanged = !string.Equals(
                body.Email.Trim(), provider.Email.Trim(), StringComparison.OrdinalIgnoreCase);

            if (emailChanged)
            {
                if (string.IsNullOrWhiteSpace(body.OtpCode))
                    return Results.BadRequest(new { message = "A verification code is required when changing the email address." });

                if (!otpStore.Verify(body.Email, body.OtpCode))
                    return Results.BadRequest(new { message = "The verification code is invalid or has expired." });
            }

            // ── Update provider record ───────────────────────────────────────

            var companyName = string.IsNullOrWhiteSpace(body.CompanyName)
                ? (provider.OrganizationName ?? provider.Name)
                : body.CompanyName.Trim();

            provider.Update(
                name:             provider.Name,
                organizationName: companyName,
                email:            body.Email.Trim(),
                phone:            body.Phone?.Trim() ?? provider.Phone,
                addressLine1:     body.AddressLine1?.Trim() ?? provider.AddressLine1,
                city:             body.City?.Trim() ?? provider.City,
                state:            body.State?.Trim() ?? provider.State,
                postalCode:       body.PostalCode?.Trim() ?? provider.PostalCode,
                isActive:         provider.IsActive,
                acceptingReferrals: provider.AcceptingReferrals,
                updatedByUserId:  null);

            // ── Step 1: Create / resolve Identity org ────────────────────────

            var orgId = await identityOrgs.EnsureProviderOrganizationAsync(
                provider.TenantId, provider.Id, companyName, ct);

            if (orgId is null)
            {
                logger.LogWarning(
                    "CC2-ENROLL Identity org creation failed for provider {ProviderId}.", provider.Id);
                return Results.Problem("Account setup could not complete. Please try again or contact support.");
            }

            // ── Step 2: Link provider to org ─────────────────────────────────

            try
            {
                await providerService.LinkOrganizationAsync(
                    provider.TenantId, provider.Id, orgId.Value, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "CC2-ENROLL Provider link failed for provider {ProviderId}.", provider.Id);
                return Results.Problem("Account setup could not complete. Please try again or contact support.");
            }

            // ── Step 3: Create active Identity user with chosen password ─────

            var registerResult = await identityOrgs.RegisterUserDirectlyAsync(
                orgId.Value,
                body.Email.Trim(),
                body.Password,
                body.FirstName.Trim(),
                body.LastName?.Trim(),
                ct);

            if (registerResult is null)
            {
                logger.LogWarning(
                    "CC2-ENROLL Identity user registration failed for provider {ProviderId}.", provider.Id);
                return Results.Problem("Account setup could not complete. Please try again or contact support.");
            }

            // ── Step 4: Activate provider → COMMON_PORTAL stage ─────────────

            try
            {
                provider.MarkCommonPortalActivated(registerResult.UserId);
                await providers.UpdateAsync(provider, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "CC2-ENROLL Provider stage transition failed for provider {ProviderId}.", provider.Id);
                // Don't fail — the user account is created; they can log in. Log for ops.
            }

            // ── Audit ────────────────────────────────────────────────────────

            _ = auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "careconnect.provider.self.enrolled",
                EventCategory = EventCategory.Administrative,
                SourceSystem  = "careconnect",
                SourceService = "enrollment-api",
                Visibility    = AuditVisibility.Tenant,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Tenant, TenantId = provider.TenantId.ToString() },
                Actor         = new AuditEventActorDto { Type = ActorType.User, Name = body.Email.Trim() },
                Entity        = new AuditEventEntityDto { Type = "Provider", Id = provider.Id.ToString() },
                Action        = "SelfEnrolled",
                Description   = $"Provider '{companyName}' self-enrolled via common portal (email: {body.Email.Trim()}).",
                Tags          = ["self-enrollment", "provider", "common-portal"],
            });

            logger.LogInformation(
                "CC2-ENROLL Provider {ProviderId} self-enrolled. UserId={UserId} OrgId={OrgId}.",
                provider.Id, registerResult.UserId, orgId.Value);

            return Results.Ok(new { success = true });
        });

        // POST /api/public/enrollment/register-firm
        // Law firm self-enrollment: creates a LAW_FIRM Identity org and active user.
        // No providerId required — law firms are not CareConnect providers.
        // No OTP required — they are creating a brand-new account.
        group.MapPost("/register-firm", async (
            HttpContext               http,
            IConfiguration           config,
            FirmEnrollmentRegisterRequest body,
            IIdentityOrganizationService identityOrgs,
            IAuditEventClient         auditClient,
            ILoggerFactory            loggerFactory,
            CancellationToken         ct) =>
        {
            var logger = loggerFactory.CreateLogger("CC2-ENROLL-FIRM.Register");

            var tenantId = ValidateTrustBoundary(http, config);
            if (tenantId is null)
                return Results.StatusCode(403);

            if (string.IsNullOrWhiteSpace(body.CompanyName))
                return Results.BadRequest(new { message = "Company name is required." });
            if (string.IsNullOrWhiteSpace(body.Email) || !IsValidEmail(body.Email))
                return Results.BadRequest(new { message = "A valid email address is required." });
            if (string.IsNullOrWhiteSpace(body.Password) || body.Password.Length < 8)
                return Results.BadRequest(new { message = "Password must be at least 8 characters." });
            if (string.IsNullOrWhiteSpace(body.FirstName))
                return Results.BadRequest(new { message = "First name is required." });

            // Use the tenantId supplied in the payload (the CareConnect tenant that owns the referral)
            // rather than the trust-boundary resolved one which may differ.
            var firmTenantId = body.TenantId != Guid.Empty ? body.TenantId : tenantId.Value;

            // Step 1: Create / resolve the LAW_FIRM org
            var orgId = await identityOrgs.EnsureLawFirmOrganizationAsync(
                firmTenantId, body.CompanyName.Trim(), body.Email.Trim(), ct);

            if (orgId is null)
            {
                logger.LogWarning(
                    "CC2-ENROLL-FIRM Identity org creation failed for firm '{CompanyName}'.", body.CompanyName);
                return Results.Problem("Account setup could not complete. Please try again or contact support.");
            }

            // Step 2: Create active user with chosen password
            var registerResult = await identityOrgs.RegisterUserDirectlyAsync(
                orgId.Value,
                body.Email.Trim(),
                body.Password,
                body.FirstName.Trim(),
                body.LastName?.Trim(),
                ct);

            if (registerResult is null)
            {
                logger.LogWarning(
                    "CC2-ENROLL-FIRM Identity user registration failed for firm '{CompanyName}'.", body.CompanyName);
                return Results.Problem("Account setup could not complete. Please try again or contact support.");
            }

            _ = auditClient.IngestAsync(new LegalSynq.AuditClient.DTOs.IngestAuditEventRequest
            {
                EventType     = "careconnect.lawfirm.self.enrolled",
                EventCategory = LegalSynq.AuditClient.Enums.EventCategory.Administrative,
                SourceSystem  = "careconnect",
                SourceService = "enrollment-api",
                Visibility    = AuditVisibility.Tenant,
                Severity      = LegalSynq.AuditClient.Enums.SeverityLevel.Info,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                Scope         = new LegalSynq.AuditClient.DTOs.AuditEventScopeDto { ScopeType = LegalSynq.AuditClient.Enums.ScopeType.Tenant, TenantId = firmTenantId.ToString() },
                Actor         = new LegalSynq.AuditClient.DTOs.AuditEventActorDto { Type = LegalSynq.AuditClient.Enums.ActorType.User, Name = body.Email.Trim() },
                Entity        = new LegalSynq.AuditClient.DTOs.AuditEventEntityDto { Type = "LawFirm", Id = orgId.Value.ToString() },
                Action        = "SelfEnrolled",
                Description   = $"Law firm '{body.CompanyName.Trim()}' self-enrolled via CareConnect portal (email: {body.Email.Trim()}).",
                Tags          = ["self-enrollment", "law-firm", "referral-portal"],
            });

            logger.LogInformation(
                "CC2-ENROLL-FIRM Law firm '{CompanyName}' self-enrolled. UserId={UserId} OrgId={OrgId}.",
                body.CompanyName, registerResult.UserId, orgId.Value);

            return Results.Ok(new { success = true });
        });
    }

    // ── Trust boundary validation ──────────────────────────────────────────────
    // Mirrors the same two-layer validation used by PublicNetworkEndpoints (BLK-SEC-02-02).

    private static Guid? ValidateTrustBoundary(HttpContext http, IConfiguration config)
    {
        var logger = http.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("CareConnect.EnrollmentTrustBoundary");

        var secret    = config["PublicTrustBoundary:InternalRequestSecret"];
        var requestId = http.Items["CorrelationId"]?.ToString() ?? http.TraceIdentifier;

        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogWarning(
                "PublicTrustBoundary:InternalRequestSecret not configured — validation DISABLED. " +
                "Path={Path} RequestId={RequestId}", http.Request.Path, requestId);
            return ResolveTenantIdRaw(http);
        }

        var gatewaySecret = http.Request.Headers["X-Internal-Gateway-Secret"].FirstOrDefault();
        if (gatewaySecret != secret)
        {
            logger.LogWarning(
                "Enrollment request rejected: gateway secret mismatch. Path={Path} RequestId={RequestId}",
                http.Request.Path, requestId);
            return null;
        }

        var tenantIdRaw = http.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var sig         = http.Request.Headers["X-Tenant-Id-Sig"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(tenantIdRaw) || string.IsNullOrWhiteSpace(sig))
        {
            logger.LogWarning(
                "Enrollment request rejected: X-Tenant-Id or X-Tenant-Id-Sig missing. Path={Path}",
                http.Request.Path);
            return null;
        }

        if (!TryValidateHmac(tenantIdRaw, sig, secret))
        {
            logger.LogWarning(
                "Enrollment request rejected: HMAC validation failed. Path={Path}", http.Request.Path);
            return null;
        }

        return Guid.TryParse(tenantIdRaw, out var tenantId) ? tenantId : null;
    }

    private static Guid? ResolveTenantIdRaw(HttpContext http)
    {
        var raw = http.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }

    private static bool TryValidateHmac(string data, string sig, string secret)
    {
        try
        {
            byte[] sigBytes;
            try { sigBytes = Convert.FromBase64String(sig); } catch { return false; }

            using var hmac     = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var       expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));

            if (sigBytes.Length != expected.Length) return false;
            return CryptographicOperations.FixedTimeEquals(expected, sigBytes);
        }
        catch { return false; }
    }

    // ── Email validation ───────────────────────────────────────────────────────

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email.Trim();
        }
        catch { return false; }
    }

    // ── OTP email template ─────────────────────────────────────────────────────

    private static string BuildOtpEmailHtml(string email, string code) => $"""
        <!DOCTYPE html>
        <html>
        <body style="font-family:sans-serif;max-width:480px;margin:0 auto;padding:24px;color:#1f2937">
          <h2 style="font-size:20px;font-weight:700;margin-bottom:8px">Verify your email address</h2>
          <p style="margin:0 0 16px">
            Use the code below to verify <strong>{System.Net.WebUtility.HtmlEncode(email)}</strong>
            during your CareConnect enrollment. This code expires in 10&nbsp;minutes.
          </p>
          <div style="background:#f3f4f6;border-radius:8px;padding:20px;text-align:center;margin:0 0 16px">
            <span style="font-size:32px;font-weight:800;letter-spacing:8px;color:#1d4ed8">{code}</span>
          </div>
          <p style="font-size:13px;color:#6b7280;margin:0">
            If you did not request this code, please ignore this email.
          </p>
        </body>
        </html>
        """;

    // ── Request / Response DTOs ────────────────────────────────────────────────

    public record EnrollmentPrefillResponse(
        Guid    ProviderId,
        string  CompanyName,
        string  CompanyType,
        string  Email,
        string  Phone,
        string  AddressLine1,
        string  City,
        string  State,
        string  PostalCode);

    public record SendOtpRequest(
        string  Email,
        Guid?   ProviderId = null);

    public record EnrollmentRegisterRequest(
        Guid    ProviderId,
        string  CompanyName,
        string  Email,
        string  Password,
        string  FirstName,
        string? LastName     = null,
        string? Phone        = null,
        string? AddressLine1 = null,
        string? City         = null,
        string? State        = null,
        string? PostalCode   = null,
        string? OtpCode      = null);

    public record FirmEnrollmentRegisterRequest(
        Guid    TenantId,
        string  CompanyName,
        string  Email,
        string  Password,
        string  FirstName,
        string? LastName     = null,
        string? Phone        = null,
        string? AddressLine1 = null,
        string? City         = null,
        string? State        = null,
        string? PostalCode   = null);
}
