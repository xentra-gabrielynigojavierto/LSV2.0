using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using BuildingBlocks.DataGovernance;
using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuditEventClient _auditClient;
    private readonly IEffectiveAccessService _effectiveAccessService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IAuditEventClient auditClient,
        IEffectiveAccessService effectiveAccessService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _auditClient = auditClient;
        _effectiveAccessService = effectiveAccessService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private string? CurrentCorrelationId =>
        _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString();

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        // Canonical audit helpers — used when a login failure must be emitted before re-throwing.
        // fire-and-observe: never awaited, never allowed to gate the primary auth response.
        var tenantCodeNorm  = (request.TenantCode ?? string.Empty).ToLowerInvariant().Trim();
        var emailNorm       = request.Email.ToLowerInvariant().Trim();

        // AUTH-CC01: Common-portal email-based tenant resolution.
        // When the portal cannot resolve a tenant from the subdomain (e.g. the common portal
        // careconnect-demo.legalsynq.com serves multiple tenants), the BFF sets ResolveByEmail=true.
        // We look up the user globally by email, find their tenant, then proceed with the normal
        // password-verification path.  Always throw UnauthorizedAccessException on any failure so
        // the caller cannot distinguish "email not found" from "wrong password".
        if (request.ResolveByEmail)
        {
            _logger.LogInformation("AUTH-CC01: email-based tenant resolution for email={EmailMasked}", PiiGuard.MaskEmail(emailNorm));
            var globalUser = await _userRepository.GetByEmailAsync(emailNorm, ct);
            if (globalUser is null || !globalUser.IsActive)
            {
                _logger.LogWarning(
                    "AUTH-CC01: LoginAsync failed: UserNotFoundOrInactive emailMasked={EmailMasked} ip={Ip}",
                    PiiGuard.MaskEmail(emailNorm), ipAddress);
                EmitLoginFailed(emailNorm, tenantCode: "common-portal", userId: null, reason: "UserNotFound", ipAddress: ipAddress);
                throw new UnauthorizedAccessException();
            }

            var globalTenant = await _tenantRepository.GetByIdAsync(globalUser.TenantId, ct);
            if (globalTenant is null || !globalTenant.IsActive)
            {
                _logger.LogWarning(
                    "AUTH-CC01: LoginAsync failed: TenantNotFoundOrInactive tenantId={TenantId} emailMasked={EmailMasked} ip={Ip}",
                    globalUser.TenantId, PiiGuard.MaskEmail(emailNorm), ipAddress);
                EmitLoginFailed(emailNorm, tenantCode: "common-portal", userId: null, reason: "TenantNotFound", ipAddress: ipAddress);
                throw new UnauthorizedAccessException();
            }

            // Re-use the tail of the normal flow (lock check, password verify, JWT issue).
            // We construct a synthetic request scoped to the resolved tenant so we can fall
            // through to the shared code below without duplication.
            request = request with { TenantCode = globalTenant.Code, TenantId = globalTenant.Id, ResolveByEmail = false };
            tenantCodeNorm = globalTenant.Code.ToLowerInvariant().Trim();
            _logger.LogInformation(
                "AUTH-CC01: Resolved tenant {TenantCode} ({TenantId}) for email={EmailMasked}",
                globalTenant.Code, globalTenant.Id, PiiGuard.MaskEmail(emailNorm));

            // Skip all tenant-lookup branches; go straight to user+lock+password checks.
            var ccUser = globalUser.IsLocked ? null : globalUser; // honour lock state below
            if (globalUser.IsLocked)
            {
                _logger.LogWarning(
                    "AUTH-CC01: LoginAsync failed: AccountLocked userId={UserId} tenantCode={TenantCode} emailMasked={EmailMasked} ip={Ip}",
                    globalUser.Id, globalTenant.Code, PiiGuard.MaskEmail(emailNorm), ipAddress);
                EmitLoginFailed(emailNorm, tenantCode: globalTenant.Code, userId: globalUser.Id.ToString(), reason: "AccountLocked", ipAddress: ipAddress);
                EmitLockedLoginBlocked(globalUser, globalTenant, ipAddress);
                throw new UnauthorizedAccessException();
            }

            var ccValid = _passwordHasher.Verify(request.Password, globalUser.PasswordHash);
            if (!ccValid)
            {
                _logger.LogWarning(
                    "AUTH-CC01: LoginAsync failed: InvalidCredentials userId={UserId} tenantCode={TenantCode} emailMasked={EmailMasked} ip={Ip}",
                    globalUser.Id, globalTenant.Code, PiiGuard.MaskEmail(emailNorm), ipAddress);
                EmitLoginFailed(emailNorm, tenantCode: globalTenant.Code, userId: globalUser.Id.ToString(), reason: "InvalidCredentials", ipAddress: ipAddress);
                throw new UnauthorizedAccessException();
            }

            var ccUserWithRoles = await _userRepository.GetByIdWithRolesAsync(globalUser.Id, ct);
            if (ccUserWithRoles is null)
            {
                EmitLoginFailed(emailNorm, tenantCode: globalTenant.Code, userId: globalUser.Id.ToString(), reason: "RoleLookupFailed", ipAddress: ipAddress);
                throw new UnauthorizedAccessException();
            }

            // Delegate the rest (role extraction, membership, JWT) to the shared tail.
            return await BuildLoginResponseAsync(ccUserWithRoles, globalTenant, request, sw, ipAddress, ct);
        }

        var tenant = await _tenantRepository.GetByCodeAsync(tenantCodeNorm, ct);

        if (tenant is null)
        {
            var upperCode = (request.TenantCode ?? string.Empty).ToUpperInvariant().Trim();
            tenant = await _tenantRepository.GetByCodeAsync(upperCode, ct);
        }

        if (tenant is null && !string.IsNullOrWhiteSpace(request.Subdomain))
        {
            var subNorm = request.Subdomain.ToLowerInvariant().Trim();
            _logger.LogInformation("Code lookup missed for {Code}, trying subdomain {Subdomain}", tenantCodeNorm, subNorm);
            tenant = await _tenantRepository.GetBySubdomainAsync(subNorm, ct);
        }

        // AUTH-B01: Final fallback — use the Tenant-service-resolved TenantId when both
        // code and subdomain lookups miss.  This handles the case where the common portal
        // (e.g. careconnect-demo.legalsynq.com) has its canonical record in the Tenant
        // service but the Identity idt_Tenants write-through row carries a different code
        // or has no subdomain populated yet.  When found this way, we skip the
        // ProvisioningStatus guard: the BFF already confirmed the tenant is active by
        // resolving it from the Tenant service, so a Pending stub in Identity is just
        // a stale write-through record, not a real provisioning-in-progress signal.
        var tenantFoundViaIdFallback = false;
        if (tenant is null && request.TenantId.HasValue && request.TenantId.Value != Guid.Empty)
        {
            _logger.LogInformation(
                "AUTH-B01: Code+subdomain lookup missed for {Code}, trying TenantId fallback {TenantId}",
                tenantCodeNorm, request.TenantId.Value);
            tenant = await _tenantRepository.GetByIdAsync(request.TenantId.Value, ct);
            if (tenant is not null)
                tenantFoundViaIdFallback = true;
        }

        if (tenant is null || !tenant.IsActive)
        {
            var reason = tenant is null ? "TenantNotFound" : "TenantInactive";
            _logger.LogWarning(
                "LoginAsync failed: branch={Reason} tenantCode={TenantCode} emailMasked={EmailMasked} ip={Ip}",
                reason, tenantCodeNorm, PiiGuard.MaskEmail(emailNorm), ipAddress);
            EmitLoginFailed(emailNorm, tenantCode: tenantCodeNorm, userId: null, reason: reason, ipAddress: ipAddress);
            throw new UnauthorizedAccessException();
        }

        // Skip provisioning-status guards when the tenant was resolved by the
        // Tenant-service-authoritative TenantId: the stub in Identity may still be
        // Pending, but the real tenant is live.
        if (!tenantFoundViaIdFallback)
        {
            if (tenant.ProvisioningStatus == ProvisioningStatus.Verifying)
            {
                EmitLoginFailed(emailNorm, tenantCode: tenantCodeNorm, userId: null, reason: "TenantVerificationRetrying", ipAddress: ipAddress);
                throw new InvalidOperationException(
                    $"Tenant '{tenantCodeNorm}' is currently verifying DNS configuration. " +
                    "This process typically completes within a few minutes. Please try again shortly.");
            }

            if (tenant.ProvisioningStatus != ProvisioningStatus.Active)
            {
                EmitLoginFailed(emailNorm, tenantCode: tenantCodeNorm, userId: null, reason: "TenantNotProvisioned", ipAddress: ipAddress);
                throw new InvalidOperationException($"Tenant '{tenantCodeNorm}' is not fully provisioned (status: {tenant.ProvisioningStatus}). Please wait for setup to complete.");
            }
        }

        var normalizedEmail = emailNorm;
        var user = await _userRepository.GetByTenantAndEmailAsync(tenant.Id, normalizedEmail, ct);
        if (user is null || !user.IsActive)
        {
            var reason = user is null ? "UserNotFound" : "UserInactive";
            _logger.LogWarning(
                "LoginAsync failed: branch={Reason} tenantCode={TenantCode} emailMasked={EmailMasked} ip={Ip}",
                reason, tenant.Code, PiiGuard.MaskEmail(normalizedEmail), ipAddress);
            EmitLoginFailed(normalizedEmail, tenantCode: tenant.Code, userId: null, reason: reason, ipAddress: ipAddress);
            throw new UnauthorizedAccessException();
        }

        // UIX-003-03: reject locked accounts (checked after IsActive so lock state is independent).
        if (user.IsLocked)
        {
            _logger.LogWarning(
                "LoginAsync failed: branch=AccountLocked userId={UserId} tenantCode={TenantCode} emailMasked={EmailMasked} ip={Ip}",
                user.Id, tenant.Code, PiiGuard.MaskEmail(normalizedEmail), ipAddress);
            EmitLoginFailed(normalizedEmail, tenantCode: tenant.Code, userId: user.Id.ToString(), reason: "AccountLocked", ipAddress: ipAddress);
            EmitLockedLoginBlocked(user, tenant, ipAddress);
            throw new UnauthorizedAccessException();
        }

        var valid = _passwordHasher.Verify(request.Password, user.PasswordHash);
        if (!valid)
        {
            _logger.LogWarning(
                "LoginAsync failed: branch=InvalidCredentials userId={UserId} tenantCode={TenantCode} emailMasked={EmailMasked} ip={Ip}",
                user.Id, tenant.Code, PiiGuard.MaskEmail(normalizedEmail), ipAddress);
            EmitLoginFailed(normalizedEmail, tenantCode: tenant.Code, userId: user.Id.ToString(), reason: "InvalidCredentials", ipAddress: ipAddress);
            throw new UnauthorizedAccessException();
        }

        var userWithRoles = await _userRepository.GetByIdWithRolesAsync(user.Id, ct);
        if (userWithRoles is null)
        {
            _logger.LogWarning(
                "LoginAsync failed: branch=RoleLookupFailed userId={UserId} tenantCode={TenantCode} emailMasked={EmailMasked} ip={Ip}",
                user.Id, tenant.Code, PiiGuard.MaskEmail(normalizedEmail), ipAddress);
            EmitLoginFailed(normalizedEmail, tenantCode: tenant.Code, userId: user.Id.ToString(), reason: "RoleLookupFailed", ipAddress: ipAddress);
            throw new UnauthorizedAccessException();
        }

        return await BuildLoginResponseAsync(userWithRoles, tenant, request, sw, ipAddress, ct);
    }

    /// <summary>
    /// Shared tail for LoginAsync: given a fully-validated user (with roles loaded) and their
    /// resolved tenant, assembles the JWT, audit event, and LoginResponse.  Called from both
    /// the normal tenant-code path and the AUTH-CC01 common-portal email-resolution path.
    /// </summary>
    private async Task<LoginResponse> BuildLoginResponseAsync(
        User   userWithRoles,
        Tenant tenant,
        LoginRequest request,
        Stopwatch sw,
        string? ipAddress,
        CancellationToken ct)
    {
        // Phase G: ScopedRoleAssignments (GLOBAL) is the sole authoritative role source.
        // UserRoles table has been dropped (migration 20260330200004).
        var roleNames = userWithRoles.ScopedRoleAssignments
            .Where(s => s.IsActive && s.ScopeType == Domain.ScopedRoleAssignment.ScopeTypes.Global)
            .Select(s => s.Role.Name)
            .ToList();

        // Load org membership for JWT context (primary org)
        var orgMembership = await _userRepository.GetPrimaryOrgMembershipAsync(userWithRoles.Id, ct);
        var org = orgMembership?.Organization;

        // LS-COR-AUT-003/006: compute effective access from the single source-of-truth model.
        // All product roles come exclusively from EffectiveAccessService (direct + group-inherited).
        var effectiveAccess = await _effectiveAccessService.GetEffectiveAccessAsync(tenant.Id, userWithRoles.Id, ct);

        // Phase H: derive org_type code from OrganizationTypeId FK (authoritative) when available;
        // fall back to the stored OrgType string for compatibility.
        // TODO [Phase H — remove OrgType string]: remove OrgType string from UserResponse once column is dropped.
        var orgTypeForResponse = org is not null
            ? (Domain.OrgTypeMapper.TryResolveCode(org.OrganizationTypeId) ?? org.OrgType)
            : null;

        // CC-AUTH-01: Law firms are intrinsically CareConnect referrers.
        // If the user belongs to a LAW_FIRM org and has no CareConnect product role yet
        // (i.e. explicit provisioning hasn't run), auto-inject the referrer role so the
        // backend product-access filter passes without a manual provisioning step.
        // Mirrors how LienOwner implies NetworkManager in the front-end access rules.
        var productRolesFlat = effectiveAccess.ProductRolesFlat;
        const string CcProductPrefix   = BuildingBlocks.Authorization.ProductCodes.SynqCareConnect + ":";
        const string CcReferrerRole    = BuildingBlocks.Authorization.ProductCodes.SynqCareConnect + ":CARECONNECT_REFERRER";
        if (string.Equals(orgTypeForResponse, Domain.OrgType.LawFirm, StringComparison.OrdinalIgnoreCase)
            && !productRolesFlat.Any(r => r.StartsWith(CcProductPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            productRolesFlat = [.. productRolesFlat, CcReferrerRole];
        }

        var (token, expiresAtUtc) = _jwtTokenService.GenerateToken(
            userWithRoles, tenant, roleNames, org, productRolesFlat,
            sessionTimeoutMinutes: tenant.SessionTimeoutMinutes,
            productCodes: effectiveAccess.Products,
            permissions: effectiveAccess.Permissions);

        var userResponse = new UserResponse(
            userWithRoles.Id,
            userWithRoles.TenantId,
            userWithRoles.Email,
            userWithRoles.FirstName,
            userWithRoles.LastName,
            userWithRoles.IsActive,
            roleNames,
            org?.Id,
            orgTypeForResponse,
            productRolesFlat);

        // Canonical audit: fire-and-observe — never throw, never gate login on audit success.
        var now = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.user.login.succeeded",
            EventCategory = EventCategory.Security,
            SourceSystem  = "identity-service",
            SourceService = "auth-api",
            Visibility    = VisibilityScope.User,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = tenant.Id.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Id        = userWithRoles.Id.ToString(),
                Type      = ActorType.User,
                Name      = $"{userWithRoles.FirstName} {userWithRoles.LastName}".Trim(),
                IpAddress = ipAddress,
            },
            Entity = new AuditEventEntityDto { Type = "User", Id = userWithRoles.Id.ToString() },
            Action      = "LoginSucceeded",
            Description = $"User (id={userWithRoles.Id}) authenticated successfully in tenant {tenant.Code}.",
            Metadata    = JsonSerializer.Serialize(new { tenantCode = tenant.Code }),
            CorrelationId  = CurrentCorrelationId,
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.user.login.succeeded", userWithRoles.Id.ToString()),
            Tags = ["auth", "login"],
        });

        // UIX-003-03: update LastLoginAtUtc. Best-effort — never gate login on this write.
        try
        {
            userWithRoles.RecordLogin();
            await _userRepository.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist LastLoginAtUtc for user {UserId}. Non-fatal.", userWithRoles.Id);
        }

        sw.Stop();
        _logger.LogInformation(
            "LoginPerf userId={UserId} tenantId={TenantId} elapsedMs={ElapsedMs} accessVersion={AccessVersion}",
            userWithRoles.Id, tenant.Id, sw.ElapsedMilliseconds, userWithRoles.AccessVersion);

        return new LoginResponse(token, expiresAtUtc, userResponse);
    }

    /// <summary>
    /// Assembles an AuthMeResponse from a validated ClaimsPrincipal.
    /// Most fields come from JWT claims; AvatarDocumentId is fetched from DB
    /// since it changes independently of the token lifecycle.
    ///
    /// UIX-003-03: validates SessionVersion from the JWT against the DB value.
    /// If the token's session_version is older than the current DB value,
    /// the session is considered force-logged-out and the request is rejected.
    /// </summary>
    public async Task<AuthMeResponse> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var userId     = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? throw new InvalidOperationException("sub claim missing");
        var email      = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email")
            ?? string.Empty;
        var tenantId   = principal.FindFirstValue("tenant_id")   ?? string.Empty;
        var tenantCode = principal.FindFirstValue("tenant_code") ?? string.Empty;
        var orgId      = principal.FindFirstValue("org_id");
        var orgType    = principal.FindFirstValue("org_type");

        var productRoles = principal.FindAll("product_roles")
            .Select(c => c.Value)
            .ToList();

        // Identity.Api uses MapInboundClaims=false, so JWT "role" claims are stored
        // in the ClaimsPrincipal with type "role" (the short JWT name), NOT with
        // ClaimTypes.Role (the long Microsoft URI).  Use the short name here so
        // that auth/me correctly reflects whatever roles are in the JWT.
        var systemRoles = principal.FindAll("role")
            .Select(c => c.Value)
            .ToList();

        // LS-ID-TNT-009: Read the user-specific effective product codes baked into the JWT
        // by EffectiveAccessService at login time. These reflect direct grants, group
        // inheritance, TenantAdmin auto-grant, and LegacyDefault — and are kept fresh by
        // the access_version stale-token check above. The product_codes claim stores
        // backend codes (e.g. "SYNQ_FUND"); map them to the same frontend-friendly codes
        // used by enabledProducts so the product switcher can apply a single filter.
        var rawUserProductCodes = principal.FindAll("product_codes")
            .Select(c => c.Value)
            .ToList();
        var userProducts = rawUserProductCodes
            .Select(code => DbToFrontendProductCode.TryGetValue(code, out var fc) ? fc : code)
            .ToList();

        // Derive expiry from the "exp" claim (Unix epoch seconds)
        var expClaim    = principal.FindFirstValue("exp");
        var expiresAtUtc = expClaim is not null && long.TryParse(expClaim, out var expUnix)
            ? DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime
            : DateTime.UtcNow.AddMinutes(60);

        // Read per-tenant idle session timeout embedded at login time.
        var timeoutClaim = principal.FindFirstValue("session_timeout_minutes");
        var sessionTimeoutMinutes = timeoutClaim is not null && int.TryParse(timeoutClaim, out var tm) ? tm : 30;

        // AvatarDocumentId is not in the JWT (changes independently) — fetch from DB.
        // UIX-003-03: also validate SessionVersion and IsLocked from DB.
        Guid? avatarDocumentId = null;
        string? phone = null;
        if (Guid.TryParse(userId, out var userGuid))
        {
            var user = await _userRepository.GetByIdAsync(userGuid, ct);

            if (user == null)
                throw new UnauthorizedAccessException("User not found.");

            // UIX-003-03: reject locked accounts immediately — they cannot use existing sessions.
            if (user.IsLocked)
                throw new UnauthorizedAccessException("Account is locked.");

            avatarDocumentId = user.AvatarDocumentId;
            phone            = user.Phone;

            // UIX-003-03: validate session version. Tokens from before a force-logout
            // or lock will have an older session_version and must be rejected.
            // If the claim is absent (old tokens before this feature), allow through.
            var sessionVersionClaim = principal.FindFirstValue("session_version");
            if (sessionVersionClaim is not null
                && int.TryParse(sessionVersionClaim, out var tokenVersion)
                && tokenVersion < user.SessionVersion)
            {
                EmitSessionInvalidated(userId, tenantId, email);
                throw new UnauthorizedAccessException("Session has been invalidated.");
            }

            // LS-COR-AUT-003: validate access version. Tokens from before an access
            // change will have a stale access_version and must be rejected.
            var accessVersionClaim = principal.FindFirstValue("access_version");
            if (accessVersionClaim is not null
                && int.TryParse(accessVersionClaim, out var tokenAccessVersion)
                && tokenAccessVersion < user.AccessVersion)
            {
                EmitAccessVersionStale(userId, tenantId, email, user.AccessVersion, tokenAccessVersion);
                throw new UnauthorizedAccessException("Access has been updated. Please re-authenticate.");
            }
        }

        // Resolve which products are enabled at the tenant level.
        // Returns frontend-friendly codes (e.g. "SynqFund", "CareConnect") so the
        // tenant portal can filter its product tiles without knowing DB internals.
        List<string> enabledProducts = [];
        if (Guid.TryParse(tenantId, out var tenantGuid))
        {
            var dbCodes = await _tenantRepository.GetEnabledProductCodesAsync(tenantGuid, ct);
            enabledProducts = dbCodes
                .Select(code => DbToFrontendProductCode.TryGetValue(code, out var fc) ? fc : code)
                .ToList();
        }

        // LS-ID-TNT-015: Extract effective permission codes from the JWT so the frontend
        // can perform permission-aware UI rendering without a separate API call.
        // Permissions are embedded at login time from role→permission assignments.
        // Frontend checks are UX-only; backend enforcement (LS-ID-TNT-012) is authoritative.
        var permissions = principal.FindAll("permissions")
            .Select(c => c.Value)
            .ToList();

        return new AuthMeResponse(
            UserId:                 userId,
            Email:                  email,
            TenantId:               tenantId,
            TenantCode:             tenantCode,
            OrgId:                  orgId,
            OrgType:                orgType,
            OrgName:                null,  // Phase 2: DB lookup by orgId for DisplayName ?? Name
            ProductRoles:           productRoles,
            SystemRoles:            systemRoles,
            ExpiresAtUtc:           expiresAtUtc,
            SessionTimeoutMinutes:  sessionTimeoutMinutes,
            AvatarDocumentId:       avatarDocumentId,
            EnabledProducts:        enabledProducts,
            Phone:                  phone,
            UserProducts:           userProducts,
            Permissions:            permissions);
    }

    // Maps the DB product Code column → the frontend ProductCode (TypeScript).
    // Keep in sync with AdminEndpoints.DbToFrontendProductCode.
    private static readonly Dictionary<string, string> DbToFrontendProductCode
        = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SYNQ_FUND"]        = "SynqFund",
        ["SYNQ_LIENS"]       = "SynqLien",
        ["SYNQ_CARECONNECT"] = "CareConnect",
        ["SYNQ_AI"]          = "SynqAI",
        ["SYNQ_INSIGHTS"]    = "SynqInsights",
        ["SYNQ_BILL"]        = "SynqBill",
        ["SYNQ_RX"]          = "SynqRx",
        ["SYNQ_PAYOUT"]      = "SynqPayout",
    };

    // ── Canonical audit helpers ────────────────────────────────────────────────

    /// <summary>
    /// Emits a <c>identity.user.login.failed</c> canonical audit event.
    ///
    /// Fire-and-observe: the returned Task is discarded. This method never throws,
    /// never awaits the ingestion call, and never gates the primary auth failure response.
    ///
    /// The failure reason is stored as metadata only. The HTTP response to the caller
    /// never reveals which specific check failed (tenant/user/password) — the caller
    /// always receives 401 Unauthorized.
    ///
    /// HIPAA §164.312(b): failed login attempts are a required audit event.
    /// </summary>
    private void EmitLoginFailed(string email, string tenantCode, string? userId, string reason, string? ipAddress = null)
    {
        var now = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.user.login.failed",
            EventCategory = EventCategory.Security,
            SourceSystem  = "identity-service",
            SourceService = "auth-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = null,
            },
            Actor = new AuditEventActorDto
            {
                Id        = userId,
                Type      = ActorType.User,
                Name      = PiiGuard.MaskEmail(email),
                IpAddress = ipAddress,
            },
            Entity      = userId is not null ? new AuditEventEntityDto { Type = "User", Id = userId } : null,
            Action      = "LoginFailed",
            Description = $"Failed login attempt for '{PiiGuard.MaskEmail(email)}' in tenant '{tenantCode}'.",
            Metadata    = System.Text.Json.JsonSerializer.Serialize(new
            {
                tenantCode,
                failureReason = reason,
            }),
            CorrelationId  = CurrentCorrelationId,
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.user.login.failed", email),
            Tags = ["auth", "login", "failure", "security"],
        });
    }

    /// <summary>
    /// LS-ID-TNT-017-002: emits <c>identity.session.invalidated</c> when a JWT's
    /// <c>session_version</c> is older than the DB value (e.g. after a force-logout).
    /// Fire-and-observe. Never throws, never gates the rejection response.
    /// </summary>
    private void EmitSessionInvalidated(string userId, string tenantId, string email)
    {
        var now = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.session.invalidated",
            EventCategory = EventCategory.Security,
            SourceSystem  = "identity-service",
            SourceService = "auth-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = tenantId,
            },
            Actor = new AuditEventActorDto
            {
                Id   = userId,
                Type = ActorType.User,
                Name = email,
            },
            Entity      = new AuditEventEntityDto { Type = "User", Id = userId },
            Action      = "SessionInvalidated",
            Description = $"Session invalidated for user '{email}' — JWT session_version is stale (force-logout or account lock).",
            Metadata    = JsonSerializer.Serialize(new { reason = "SessionVersionStale" }),
            CorrelationId  = CurrentCorrelationId,
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(
                now, "identity-service", "identity.session.invalidated", userId),
            Tags = ["auth", "session", "invalidated", "security"],
        });
    }

    /// <summary>
    /// LS-ID-TNT-017-002: emits <c>identity.access.version.stale</c> when a JWT's
    /// <c>access_version</c> is older than the DB value (e.g. after a permission change).
    /// Signals that the user must re-authenticate to acquire a fresh JWT with updated claims.
    /// Fire-and-observe. Never throws, never gates the rejection response.
    /// </summary>
    private void EmitAccessVersionStale(
        string userId, string tenantId, string email,
        int currentAccessVersion, int tokenAccessVersion)
    {
        var now = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.access.version.stale",
            EventCategory = EventCategory.Security,
            SourceSystem  = "identity-service",
            SourceService = "auth-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = tenantId,
            },
            Actor = new AuditEventActorDto
            {
                Id   = userId,
                Type = ActorType.User,
                Name = email,
            },
            Entity      = new AuditEventEntityDto { Type = "User", Id = userId },
            Action      = "AccessVersionStale",
            Description = $"Stale access_version detected for user '{email}' — re-authentication required (permission change since last login).",
            Metadata    = JsonSerializer.Serialize(new
            {
                reason               = "AccessVersionStale",
                tokenAccessVersion,
                currentAccessVersion,
            }),
            CorrelationId  = CurrentCorrelationId,
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(
                now, "identity-service", "identity.access.version.stale", userId),
            Tags = ["auth", "access-version", "stale", "security", "re-auth"],
        });
    }

    /// <summary>
    /// UIX-003-03: emits a dedicated <c>identity.user.login.blocked</c> event when a locked
    /// account attempts to authenticate. Separate from the generic login.failed event so
    /// security dashboards can distinguish intentional locks from bad credentials.
    /// Fire-and-observe.
    /// </summary>
    private void EmitLockedLoginBlocked(User user, Tenant tenant, string? ipAddress)
    {
        var now = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.user.login.blocked",
            EventCategory = EventCategory.Security,
            SourceSystem  = "identity-service",
            SourceService = "auth-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = tenant.Id.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Id        = user.Id.ToString(),
                Type      = ActorType.User,
                Name      = PiiGuard.MaskEmail(user.Email),
                IpAddress = ipAddress,
            },
            Entity      = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
            Action      = "LoginBlocked",
            Description = $"Login attempt blocked for locked account (userId={user.Id}) in tenant {tenant.Code}.",
            Metadata    = JsonSerializer.Serialize(new { tenantCode = tenant.Code, userId = user.Id.ToString(), reason = "AccountLocked" }),
            CorrelationId  = CurrentCorrelationId,
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.user.login.blocked", user.Id.ToString()),
            Tags = ["auth", "login", "blocked", "security", "locked"],
        });
    }

}
