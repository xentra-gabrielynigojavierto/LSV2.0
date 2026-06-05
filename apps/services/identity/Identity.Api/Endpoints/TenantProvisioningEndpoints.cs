using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Endpoints;

/// <summary>
/// TENANT-B12 — Internal Identity provisioning endpoint.
///
/// Called by the Tenant service's <c>IIdentityProvisioningAdapter</c> after the
/// canonical Tenant record has been created in the Tenant service DB.
///
/// This endpoint does all the Identity-side setup work for a new tenant:
///   1. Creates the Identity.Tenant entity (using the tenantId supplied by Tenant service)
///   2. Creates the default admin Organization
///   3. Creates the admin User with a secure temporary password
///   4. Creates UserOrganizationMembership
///   5. Creates ScopedRoleAssignment (TenantAdmin role)
///   6. Triggers DNS/subdomain provisioning (ITenantProvisioningService)
///   7. Triggers product provisioning (IProductProvisioningService) if products supplied
///
/// Auth:   X-Provisioning-Token header must match TenantService:ProvisioningSecret config.
///         When ProvisioningSecret is empty/unset, the token check is skipped (dev mode).
///
/// This endpoint does NOT call the dual-write sync back to Tenant service.
/// The Tenant service is now the canonical source; this endpoint is a downstream
/// provisioning hook, not a tenant creator.
///
/// Route:  POST /api/internal/tenant-provisioning/provision
/// </summary>
public static class TenantProvisioningEndpoints
{
    public static void MapTenantProvisioningEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/internal/tenant-provisioning/provision", async (
            HttpContext                  httpContext,
            ProvisionTenantRequest       body,
            IdentityDbContext            db,
            IPasswordHasher              passwordHasher,
            ITenantProvisioningService   provisioningService,
            IProductProvisioningService  productProvisioningEngine,
            IUserMembershipService       membershipService,          // BLK-ID-02
            IConfiguration              configuration,
            ILoggerFactory              loggerFactory,
            CancellationToken           ct) =>
        {
            var log = loggerFactory.CreateLogger("Identity.Api.TenantProvisioningEndpoints");

            // ── Token guard ───────────────────────────────────────────────────
            var secret        = configuration["TenantService:ProvisioningSecret"];
            var incomingToken = httpContext.Request.Headers["X-Provisioning-Token"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(secret) &&
                !string.Equals(incomingToken, secret, StringComparison.Ordinal))
            {
                log.LogWarning(
                    "[TenantProvisioning] Rejected — invalid X-Provisioning-Token from {RemoteIp}",
                    httpContext.Connection.RemoteIpAddress);
                return Results.Unauthorized();
            }

            // ── Validate ─────────────────────────────────────────────────────
            if (body.TenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId is required." });

            if (string.IsNullOrWhiteSpace(body.Code))
                return Results.BadRequest(new { error = "code is required." });

            if (string.IsNullOrWhiteSpace(body.AdminEmail))
                return Results.BadRequest(new { error = "adminEmail is required." });

            var code       = body.Code.Trim().ToLowerInvariant();
            var emailNorm  = body.AdminEmail.ToLowerInvariant().Trim();
            var displayName = body.DisplayName?.Trim() ?? code;

            // ── Uniqueness checks ──────────────────────────────────────────
            var codeExists = await db.Tenants.AnyAsync(t => t.Code == code, ct);
            if (codeExists)
            {
                log.LogWarning(
                    "[TenantProvisioning] Tenant code '{Code}' already exists in Identity DB (tenantId={TenantId}). Skipping duplicate create.",
                    code, body.TenantId);
                return Results.Conflict(new { error = $"Tenant code '{code}' already exists in Identity." });
            }

            var emailExists = await db.Users.AnyAsync(u => u.Email == emailNorm, ct);
            if (emailExists)
                return Results.Conflict(new { error = $"User '{emailNorm}' already exists." });

            // ── Resolve org type ───────────────────────────────────────────
            var orgTypeId = (body.OrgType ?? "").ToUpperInvariant() switch
            {
                "PROVIDER"   => new Guid("70000000-0000-0000-0000-000000000003"),
                "FUNDER"     => new Guid("70000000-0000-0000-0000-000000000004"),
                "LIEN_OWNER" => new Guid("70000000-0000-0000-0000-000000000005"),
                _            => new Guid("70000000-0000-0000-0000-000000000002"),
            };

            // ── Generate temporary password ────────────────────────────────
            var tempPassword = GenerateTemporaryPassword();
            var passwordHash = passwordHasher.Hash(tempPassword);

            // ── Create Identity-side entities ──────────────────────────────
            // Use the tenantId supplied by Tenant service so both DBs share the same UUID.
            var identityTenant = Tenant.Rehydrate(
                id:          body.TenantId,
                code:        code,
                displayName: displayName,
                status:      "Active",
                subdomain:   body.Subdomain,
                addressLine1:   body.AddressLine1,
                city:           body.City,
                state:          body.State,
                postalCode:     body.PostalCode,
                latitude:       body.Latitude,
                longitude:      body.Longitude,
                geoPointSource: body.GeoPointSource ?? "nominatim");

            db.Tenants.Add(identityTenant);

            var org = Organization.Create(
                tenantId:           identityTenant.Id,
                name:               displayName,
                organizationTypeId: orgTypeId,
                displayName:        displayName);
            db.Organizations.Add(org);

            var user = User.Create(
                tenantId:     identityTenant.Id,
                email:        emailNorm,
                passwordHash: passwordHash,
                firstName:    body.AdminFirstName?.Trim() ?? "Admin",
                lastName:     body.AdminLastName?.Trim()  ?? "User");
            db.Users.Add(user);

            var membership = UserOrganizationMembership.Create(
                userId:         user.Id,
                organizationId: org.Id,
                memberRole:     MemberRole.Admin);
            db.UserOrganizationMemberships.Add(membership);

            // Save tenant, org, user, and org membership in one batch.
            // Role assignment is done separately via IUserMembershipService (BLK-ID-02).
            await db.SaveChangesAsync(ct);

            log.LogInformation(
                "[TenantProvisioning] Identity entities created for TenantId={TenantId}, Code={Code}, AdminEmail={Email}",
                body.TenantId, code, emailNorm);

            // ── BLK-ID-02: Assign roles via the formal membership service ─────
            // Idempotent — safe on retry. Delegates all ScopedRoleAssignment
            // creation to UserMembershipService (no direct DB writes here).
            // Always assign TenantAdmin on provisioning.
            var rolesResult = await membershipService.AssignRolesAsync(
                new AssignRolesCommand(
                    UserId:   user.Id,
                    TenantId: identityTenant.Id,
                    Roles:    ["TenantAdmin"]),
                ct);

            if (rolesResult.AssignedRoles.Count > 0)
                log.LogInformation(
                    "[TenantProvisioning] Roles assigned to user {UserId}: [{Roles}]",
                    user.Id, string.Join(", ", rolesResult.AssignedRoles));

            if (rolesResult.SkippedDuplicates.Count > 0)
                log.LogWarning(
                    "[TenantProvisioning] Duplicate role assignments skipped for user {UserId}: [{Roles}]",
                    user.Id, string.Join(", ", rolesResult.SkippedDuplicates));

            // ── Provision subdomain (DNS + TenantDomain record) ───────────
            var provResult = await provisioningService.ProvisionAsync(identityTenant, ct);

            if (provResult.Success)
            {
                log.LogInformation("[TenantProvisioning] Subdomain provisioned for {Code}: {Hostname}",
                    code, provResult.Hostname);
            }
            else
            {
                log.LogWarning("[TenantProvisioning] Subdomain provisioning failed for {Code}: {Reason}",
                    code, provResult.ErrorMessage);
            }

            // ── Product provisioning ───────────────────────────────────────
            var productResults = new List<object>();
            if (body.Products is { Count: > 0 })
            {
                foreach (var rawCode in body.Products)
                {
                    try
                    {
                        var pr = await productProvisioningEngine.ProvisionAsync(
                            new ProvisionProductRequest(identityTenant.Id, rawCode, true), ct);
                        productResults.Add(new { productCode = rawCode, enabled = pr.Enabled });
                    }
                    catch (Exception ex)
                    {
                        log.LogWarning(ex, "[TenantProvisioning] Product provisioning for {ProductCode} failed", rawCode);
                    }
                }
            }

            // ── Respond ────────────────────────────────────────────────────
            return Results.Ok(new
            {
                tenantId           = identityTenant.Id,
                adminUserId        = user.Id,
                adminEmail         = user.Email,
                temporaryPassword  = tempPassword,
                subdomain          = identityTenant.Subdomain,
                hostname           = provResult.Hostname,
                provisioningStatus = identityTenant.ProvisioningStatus.ToString(),
                productsProvisioned = productResults,
            });
        });
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$";
        var rng = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        return new string(rng.Select(b => chars[b % chars.Length]).ToArray())
               + "Aa1!";
    }

    internal record ProvisionTenantRequest(
        Guid     TenantId,
        string?  Code,
        string?  DisplayName,
        string?  OrgType,
        string?  AdminEmail,
        string?  AdminFirstName,
        string?  AdminLastName,
        string?  Subdomain       = null,
        string?  AddressLine1    = null,
        string?  City            = null,
        string?  State           = null,
        string?  PostalCode      = null,
        double?  Latitude        = null,
        double?  Longitude       = null,
        string?  GeoPointSource  = null,
        List<string>? Products   = null);
}
