# LegalSynq — Authentication, JWT Generation & Capability Authorization
## Implementation Reference — Identity / Core Layer

---

## 1. JWT Design

### Claim Structure

| Claim key | Type | Source | Required |
|---|---|---|---|
| `sub` | `string` (GUID) | `Users.Id` | Always |
| `email` | `string` | `Users.Email` | Always |
| `jti` | `string` (GUID) | `Guid.NewGuid()` | Always — token replay prevention |
| `nbf` | `long` (Unix epoch) | `DateTime.UtcNow` | Always |
| `exp` | `long` (Unix epoch) | `now + ExpiryMinutes` | Always |
| `iss` | `string` | `appsettings:Jwt:Issuer` | Always |
| `aud` | `string` | `appsettings:Jwt:Audience` | Always |
| `tenant_id` | `string` (GUID) | `Tenants.Id` | Always |
| `tenant_code` | `string` | `Tenants.Code` | Always |
| `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` | `string` (one per role) | `UserRoles → Roles.Name` | When user has system roles |
| `org_id` | `string` (GUID) | `UserOrganizationMemberships.OrganizationId` | When membership exists |
| `org_type` | `string` | `Organizations.OrgType` | When membership exists |
| `product_roles` | `string` (one claim per role) | Computed from org products + role filter | When org has enabled products |

### Encoding Strategy for `product_roles`

Multiple product roles are encoded as **multiple claims with the same key**, not as a JSON array string. This is standard JWT claim behavior and is what `.NET System.IdentityModel.Tokens.Jwt` emits and reads correctly.

In the raw JWT JSON payload:
```json
{
  "sub": "7b657820-708a-4863-b18f-22ba7b15c6c3",
  "email": "admin@legalsynq.com",
  "jti": "05961d08-bc6e-4156-9225-6432a26b7227",
  "tenant_id": "20000000-0000-0000-0000-000000000001",
  "tenant_code": "LEGALSYNQ",
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "PlatformAdmin",
  "org_id": "40000000-0000-0000-0000-000000000001",
  "org_type": "INTERNAL",
  "product_roles": "SYNQFUND_APPLICANT_PORTAL",
  "nbf": 1774733511,
  "exp": 1774762311,
  "iss": "legalsynq-identity",
  "aud": "legalsynq-platform"
}
```

When a user has multiple product roles (e.g., a LAW_FIRM user):
```json
{
  "product_roles": ["CARECONNECT_REFERRER", "SYNQFUND_REFERRER", "SYNQLIEN_SELLER"]
}
```

JWT libraries serialize multiple values for the same claim key as a JSON array when there are two or more, and as a plain string when there is exactly one. Consumers must handle both forms using `ClaimsPrincipal.FindAll("product_roles")`.

### Size Considerations

Estimated payload size for a fully-loaded LAW_FIRM user with 3 product roles:
- All base claims: ~400 bytes
- 3 `product_roles` claims: ~120 bytes
- HMAC-SHA256 signature: 43 bytes (base64url)
- **Total token: ~750–900 bytes** — well within typical header limits (8KB)

Maximum theoretical payload (all 8 product roles): ~1.1KB. No concern for the foreseeable schema.

**Rule:** Capabilities are never added to the JWT. The current maximum product role set is 8 codes. Even if expanded to 50, the token remains well under 2KB.

---

## 2. Auth Service Implementation

### Login Flow — Step by Step

```
POST /api/auth/login  {tenantCode, email, password}
│
├── 1. Resolve tenant
│   SELECT * FROM Tenants WHERE Code = @tenantCode AND IsActive = 1
│   → 401 if not found or inactive
│
├── 2. Resolve user
│   SELECT * FROM Users WHERE TenantId = @tenantId AND Email = @email AND IsActive = 1
│   → 401 if not found or inactive
│
├── 3. Verify password
│   BCrypt.Verify(password, user.PasswordHash)
│   → 401 if invalid
│
├── 4. Load user + system roles
│   SELECT Users + UserRoles + Roles WHERE Users.Id = @userId
│   → roles list for ClaimTypes.Role
│
├── 5. Load primary org membership
│   SELECT TOP 1 UserOrganizationMemberships + Organizations
│     + OrganizationProducts + Products + ProductRoles
│   WHERE UserId = @userId AND IsActive = 1
│   ORDER BY JoinedAtUtc ASC
│   → may be null (no org context, JWT omits org claims)
│
├── 6. Compute product roles
│   org.OrganizationProducts
│     .Where(op => op.IsEnabled)
│     .SelectMany(op => op.Product.ProductRoles)
│     .Where(pr => pr.IsActive &&
│             (pr.EligibleOrgType is null || pr.EligibleOrgType == org.OrgType))
│     .Select(pr => pr.Code).Distinct()
│
├── 7. Generate JWT
│   → build claims → sign → return (token, expiresAtUtc)
│
└── 8. Return LoginResponse
    { accessToken, expiresAtUtc, user: UserResponse }
```

### EF Queries Required

**Query 1 — Tenant lookup (tenant repository):**
```csharp
await _context.Tenants
    .AsNoTracking()
    .FirstOrDefaultAsync(t => t.Code == code, ct);
```
Index used: `IX_Tenants_Code` (unique).

**Query 2 — User lookup by tenant + email:**
```csharp
await _context.Users
    .AsNoTracking()
    .FirstOrDefaultAsync(u => u.TenantId == tenantId &&
                              u.Email == normalizedEmail, ct);
```
Index used: `IX_Users_TenantId_Email` (unique).

**Query 3 — User + system roles:**
```csharp
await _context.Users
    .Include(u => u.UserRoles)
        .ThenInclude(ur => ur.Role)
    .FirstOrDefaultAsync(u => u.Id == userId, ct);
```

**Query 4 — Primary org membership with product chain:**
```csharp
await _context.UserOrganizationMemberships
    .Include(m => m.Organization)
        .ThenInclude(o => o.OrganizationProducts)
            .ThenInclude(op => op.Product)
                .ThenInclude(p => p.ProductRoles)
    .Where(m => m.UserId == userId && m.IsActive)
    .OrderBy(m => m.JoinedAtUtc)
    .FirstOrDefaultAsync(ct);
```
Indexes used: `IX_UserOrganizationMemberships_UserId_IsActive`.

Note: This query triggers EF Core's multi-collection include warning. It is safe as a single query at login time. See Section 9 for splitting strategy if login latency becomes a concern.

### Current LoginRequest / LoginResponse DTOs

```csharp
// Input
public record LoginRequest(
    string TenantCode,
    string Email,
    string Password);

// Response envelope
public record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserResponse User);

// User portion of response
public record UserResponse(
    Guid Id,
    Guid TenantId,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    List<string> Roles,
    Guid? OrganizationId = null,
    string? OrgType = null,
    List<string>? ProductRoles = null);
```

### Error Handling

| Condition | Response |
|---|---|
| Tenant not found or inactive | `401 Unauthorized` — do not reveal which field failed |
| User not found or inactive | `401 Unauthorized` |
| Password mismatch | `401 Unauthorized` |
| User has no org membership | `200 OK` — JWT issued without `org_id`/`org_type`/`product_roles` |
| JWT signing key missing | `500` — configuration error, never returns partial token |
| Database unavailable | `503` — caught at middleware level |

Always return the same 401 shape for all authentication failures. Do not distinguish between "user not found" and "password wrong" — this prevents user enumeration.

---

## 3. JWT Generation Code (C#)

### Current `JwtTokenService` — Annotated

```csharp
public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    public JwtTokenService(IConfiguration configuration) => _configuration = configuration;

    public (string Token, DateTime ExpiresAtUtc) GenerateToken(
        User user,
        Tenant tenant,
        IEnumerable<string> roles,
        Organization? organization = null,
        IEnumerable<string>? productRoles = null)
    {
        var section = _configuration.GetSection("Jwt");

        var issuer       = section["Issuer"]      ?? "legalsynq-identity";
        var audience     = section["Audience"]    ?? "legalsynq-platform";
        var signingKey   = section["SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var expiryMinutes = int.TryParse(section["ExpiryMinutes"], out var m) ? m : 480;

        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // ── Base claims (always present) ─────────────────────────────────────
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new("tenant_id",   tenant.Id.ToString()),
            new("tenant_code", tenant.Code),
        };

        // ── System roles (ClaimTypes.Role for [Authorize(Roles=...)]) ─────────
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        // ── Org context (omitted when no membership) ──────────────────────────
        if (organization is not null)
        {
            claims.Add(new Claim("org_id",   organization.Id.ToString()));
            claims.Add(new Claim("org_type", organization.OrgType));
        }

        // ── Product roles (one claim per code — handled as array by consumers) ─
        foreach (var pr in productRoles ?? [])
            claims.Add(new Claim("product_roles", pr));

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var token = new JwtSecurityToken(
            issuer:            issuer,
            audience:          audience,
            claims:            claims,
            notBefore:         DateTime.UtcNow,
            expires:           expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
```

### Signing Configuration

**Development (`appsettings.Development.json`):**
```json
{
  "Jwt": {
    "SigningKey": "dev-only-signing-key-minimum-32-chars-long!",
    "Issuer": "legalsynq-identity",
    "Audience": "legalsynq-platform",
    "ExpiryMinutes": 480
  }
}
```

**Production:** `SigningKey` must be a secret of at least 512 bits (64 characters). Set via environment variable / secrets manager. Never commit to source control.

### Expiration Strategy

| Token type | Expiry | Rationale |
|---|---|---|
| Access token | 480 minutes (8 hours) | Covers a full work day without forced re-login |
| Future: Refresh token | 30 days | Not yet implemented |
| Future: Party session token | 15 minutes | Short-lived single-purpose for injured party portal |

`ClockSkew = TimeSpan.Zero` is set at the gateway to prevent tokens from being accepted after expiry. Do not relax this setting.

---

## 4. Authorization Middleware / Helpers

### Claim Extraction — `UserContext`

Create a `UserContext` value object that every service reads from the JWT claims on each request. This avoids scattering `HttpContext.User.FindFirst(...)` calls across the codebase.

```csharp
// Identity.Application/Models/UserContext.cs
namespace Identity.Application.Models;

public sealed record UserContext
{
    public Guid UserId       { get; init; }
    public string Email      { get; init; } = string.Empty;
    public Guid TenantId     { get; init; }
    public string TenantCode { get; init; } = string.Empty;
    public Guid? OrgId       { get; init; }
    public string? OrgType   { get; init; }
    public IReadOnlyList<string> SystemRoles  { get; init; } = [];
    public IReadOnlyList<string> ProductRoles { get; init; } = [];

    public bool IsPlatformAdmin =>
        SystemRoles.Contains("PlatformAdmin", StringComparer.OrdinalIgnoreCase);
}
```

### `IUserContextAccessor` Interface

```csharp
// Identity.Application/Interfaces/IUserContextAccessor.cs
namespace Identity.Application.Interfaces;

public interface IUserContextAccessor
{
    UserContext Current { get; }
}
```

### Implementation — Reads from `IHttpContextAccessor`

```csharp
// Identity.Infrastructure/Auth/UserContextAccessor.cs
using System.Security.Claims;
using Identity.Application.Interfaces;
using Identity.Application.Models;
using Microsoft.AspNetCore.Http;

namespace Identity.Infrastructure.Auth;

public sealed class UserContextAccessor : IUserContextAccessor
{
    private readonly IHttpContextAccessor _http;
    public UserContextAccessor(IHttpContextAccessor http) => _http = http;

    public UserContext Current
    {
        get
        {
            var user = _http.HttpContext?.User
                ?? throw new InvalidOperationException("No HTTP context available.");

            return new UserContext
            {
                UserId    = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                                ?? user.FindFirstValue("sub")
                                ?? throw new InvalidOperationException("sub claim missing")),
                Email      = user.FindFirstValue(ClaimTypes.Email)
                                ?? user.FindFirstValue("email") ?? string.Empty,
                TenantId   = Guid.Parse(user.FindFirstValue("tenant_id")
                                ?? throw new InvalidOperationException("tenant_id claim missing")),
                TenantCode = user.FindFirstValue("tenant_code") ?? string.Empty,
                OrgId      = Guid.TryParse(user.FindFirstValue("org_id"), out var g) ? g : null,
                OrgType    = user.FindFirstValue("org_type"),
                SystemRoles  = user.FindAll(ClaimTypes.Role)
                                   .Select(c => c.Value).ToList().AsReadOnly(),
                ProductRoles = user.FindAll("product_roles")
                                   .Select(c => c.Value).ToList().AsReadOnly(),
            };
        }
    }
}
```

### DI Registration (per downstream service)

```csharp
// In each service's Program.cs or DependencyInjection.cs
services.AddHttpContextAccessor();
services.AddScoped<IUserContextAccessor, UserContextAccessor>();
services.AddScoped<ICapabilityService, CapabilityService>();
```

---

## 5. Capability Resolution Logic

### Design Principle

Capabilities are resolved **per-request** from `product_roles` in the JWT. They are never stored in the token. The resolution path is:

```
JWT product_roles (string codes)
  → look up ProductRole IDs from code set
  → join RoleCapabilities
  → select Capability.Code
  → cache by product_role_code_set key
```

### `ICapabilityService` Interface

```csharp
// Identity.Application/Interfaces/ICapabilityService.cs
namespace Identity.Application.Interfaces;

public interface ICapabilityService
{
    Task<bool> HasCapabilityAsync(
        IReadOnlyList<string> productRoleCodes,
        string capabilityCode,
        CancellationToken ct = default);

    Task<IReadOnlySet<string>> GetCapabilitiesAsync(
        IReadOnlyList<string> productRoleCodes,
        CancellationToken ct = default);
}
```

### Implementation with `IMemoryCache`

```csharp
// Identity.Infrastructure/Auth/CapabilityService.cs
using Identity.Application.Interfaces;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Identity.Infrastructure.Auth;

public sealed class CapabilityService : ICapabilityService
{
    // Cache key: sorted, pipe-delimited product role codes.
    // e.g., "CARECONNECT_REFERRER|SYNQFUND_REFERRER"
    // TTL: 5 minutes. Invalidate only on product role definition changes (rare).
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IdentityDbContext _db;
    private readonly IMemoryCache _cache;

    public CapabilityService(IdentityDbContext db, IMemoryCache cache)
    {
        _db    = db;
        _cache = cache;
    }

    public async Task<bool> HasCapabilityAsync(
        IReadOnlyList<string> productRoleCodes,
        string capabilityCode,
        CancellationToken ct = default)
    {
        var caps = await GetCapabilitiesAsync(productRoleCodes, ct);
        return caps.Contains(capabilityCode);
    }

    public async Task<IReadOnlySet<string>> GetCapabilitiesAsync(
        IReadOnlyList<string> productRoleCodes,
        CancellationToken ct = default)
    {
        if (productRoleCodes.Count == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var cacheKey = BuildCacheKey(productRoleCodes);

        if (_cache.TryGetValue(cacheKey, out IReadOnlySet<string>? cached) && cached is not null)
            return cached;

        // Single JOIN query: ProductRoles → RoleCapabilities → Capabilities
        var caps = await _db.RoleCapabilities
            .AsNoTracking()
            .Include(rc => rc.ProductRole)
            .Include(rc => rc.Capability)
            .Where(rc => productRoleCodes.Contains(rc.ProductRole.Code)
                      && rc.ProductRole.IsActive
                      && rc.Capability.IsActive)
            .Select(rc => rc.Capability.Code)
            .Distinct()
            .ToListAsync(ct);

        IReadOnlySet<string> result =
            new HashSet<string>(caps, StringComparer.OrdinalIgnoreCase);

        _cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    private static string BuildCacheKey(IReadOnlyList<string> codes)
        => "caps:" + string.Join("|", codes.Order(StringComparer.OrdinalIgnoreCase));
}
```

### SQL Generated by the Capability Query

```sql
SELECT DISTINCT c.`Code`
FROM `RoleCapabilities` rc
INNER JOIN `ProductRoles` pr ON rc.`ProductRoleId` = pr.`Id`
INNER JOIN `Capabilities` c  ON rc.`CapabilityId` = c.`Id`
WHERE pr.`Code` IN ('CARECONNECT_REFERRER', 'SYNQFUND_REFERRER')
  AND pr.`IsActive` = 1
  AND c.`IsActive`  = 1
```

Indexes covering this query:
- `RoleCapabilities(ProductRoleId)` — PK covers ProductRoleId as leading column
- `RoleCapabilities(CapabilityId)` — explicit index
- `ProductRoles(Code)` — unique index

### Usage Pattern in a Service

```csharp
public async Task<ReferralId> CreateReferralAsync(CreateReferralCommand cmd, CancellationToken ct)
{
    var ctx = _userContextAccessor.Current;

    // PlatformAdmin bypass — checked before capability resolution
    if (!ctx.IsPlatformAdmin)
    {
        var can = await _capabilityService
            .HasCapabilityAsync(ctx.ProductRoles, "referral:create", ct);
        if (!can)
            throw new ForbiddenException("referral:create");
    }

    // ... create referral
}
```

---

## 6. Authorization Usage Patterns

### CareConnect — Create Referral (LAW_FIRM / CARECONNECT_REFERRER)

```csharp
// CareConnect.Api/Endpoints/ReferralEndpoints.cs
app.MapPost("/api/referrals", async (
    CreateReferralRequest req,
    IReferralService svc,
    IUserContextAccessor ctx,
    ICapabilityService caps,
    CancellationToken ct) =>
{
    var user = ctx.Current;

    if (!user.IsPlatformAdmin)
    {
        if (!await caps.HasCapabilityAsync(user.ProductRoles, "referral:create", ct))
            return Results.Forbid();
    }

    // Ensure the org is a LAW_FIRM (defense in depth beyond the role filter)
    if (user.OrgType != OrgType.LawFirm && !user.IsPlatformAdmin)
        return Results.Forbid();

    var referralId = await svc.CreateReferralAsync(req.ToCommand(user), ct);
    return Results.Created($"/api/referrals/{referralId}", new { Id = referralId });
})
.RequireAuthorization();
```

### CareConnect — Provider Accepting Referral (PROVIDER / CARECONNECT_RECEIVER)

```csharp
app.MapPost("/api/referrals/{id}/accept", async (
    Guid id,
    IReferralService svc,
    IUserContextAccessor ctx,
    ICapabilityService caps,
    CancellationToken ct) =>
{
    var user = ctx.Current;

    if (!user.IsPlatformAdmin)
    {
        if (!await caps.HasCapabilityAsync(user.ProductRoles, "referral:accept", ct))
            return Results.Forbid();

        // Referral must be addressed to the user's org (cross-org boundary guard)
        var referral = await svc.GetAsync(id, ct);
        if (referral.ReceiverOrgId != user.OrgId)
            return Results.Forbid();
    }

    await svc.AcceptAsync(id, user.UserId, ct);
    return Results.Ok();
})
.RequireAuthorization();
```

### SynqFund — Funder Approving Application (FUNDER / SYNQFUND_FUNDER)

```csharp
app.MapPost("/api/applications/{id}/approve", async (
    Guid id,
    IApplicationService svc,
    IUserContextAccessor ctx,
    ICapabilityService caps,
    CancellationToken ct) =>
{
    var user = ctx.Current;

    if (!user.IsPlatformAdmin)
    {
        if (!await caps.HasCapabilityAsync(user.ProductRoles, "application:approve", ct))
            return Results.Forbid();

        // Application must be addressed to this funder org
        var app = await svc.GetAsync(id, ct);
        if (app.FunderOrgId != user.OrgId)
            return Results.Forbid();
    }

    await svc.ApproveAsync(id, user.UserId, ct);
    return Results.Ok();
})
.RequireAuthorization();
```

### SynqFund — Law Firm Creating Application (LAW_FIRM / SYNQFUND_REFERRER)

```csharp
app.MapPost("/api/applications", async (
    CreateApplicationRequest req,
    IApplicationService svc,
    IUserContextAccessor ctx,
    ICapabilityService caps,
    CancellationToken ct) =>
{
    var user = ctx.Current;

    if (!user.IsPlatformAdmin)
    {
        if (!await caps.HasCapabilityAsync(user.ProductRoles, "application:create", ct))
            return Results.Forbid();
    }

    var applicationId = await svc.CreateAsync(req.ToCommand(user), ct);
    return Results.Created($"/api/applications/{applicationId}", new { Id = applicationId });
})
.RequireAuthorization();
```

### SynqLien — Provider Creating Lien (PROVIDER / SYNQLIEN_SELLER)

```csharp
app.MapPost("/api/liens", async (
    CreateLienRequest req,
    ILienService svc,
    IUserContextAccessor ctx,
    ICapabilityService caps,
    CancellationToken ct) =>
{
    var user = ctx.Current;

    if (!user.IsPlatformAdmin)
    {
        if (!await caps.HasCapabilityAsync(user.ProductRoles, "lien:create", ct))
            return Results.Forbid();
    }

    var lienId = await svc.CreateAsync(req.ToCommand(user), ct);
    return Results.Created($"/api/liens/{lienId}", new { Id = lienId });
})
.RequireAuthorization();
```

### SynqLien — Lien Owner Purchasing Lien (LIEN_OWNER / SYNQLIEN_BUYER)

```csharp
app.MapPost("/api/liens/{id}/purchase", async (
    Guid id,
    ILienService svc,
    IUserContextAccessor ctx,
    ICapabilityService caps,
    CancellationToken ct) =>
{
    var user = ctx.Current;

    if (!user.IsPlatformAdmin)
    {
        if (!await caps.HasCapabilityAsync(user.ProductRoles, "lien:purchase", ct))
            return Results.Forbid();
    }

    await svc.PurchaseAsync(id, user.UserId, user.OrgId!.Value, ct);
    return Results.Ok();
})
.RequireAuthorization();
```

### Extension Method for Clean Call Sites

To reduce boilerplate at every endpoint:

```csharp
// Shared/Auth/CapabilityExtensions.cs
public static class CapabilityExtensions
{
    public static async Task<IResult?> ForbidIfLacksAsync(
        this ICapabilityService caps,
        UserContext user,
        string capabilityCode,
        CancellationToken ct = default)
    {
        if (user.IsPlatformAdmin) return null;
        return await caps.HasCapabilityAsync(user.ProductRoles, capabilityCode, ct)
            ? null
            : Results.Forbid();
    }
}

// Usage:
if (await caps.ForbidIfLacksAsync(user, "lien:purchase", ct) is { } forbidden)
    return forbidden;
```

---

## 7. Backward Compatibility Layer

### Existing `[Authorize(Roles = "...")]` — Continues Working

The gateway validates the JWT and passes all claims to downstream services. The `ClaimTypes.Role` claim is populated from `UserRoles → Roles.Name` exactly as before.

```csharp
// This still works on any endpoint in any service:
[Authorize(Roles = "PlatformAdmin")]
[Authorize(Roles = "TenantAdmin")]
```

No changes to existing role-based authorization are required. The new capability model is additive.

### Combining Role-Based and Capability-Based Checks

There are two valid patterns for mixed authorization:

**Pattern A — Attribute + in-body check (recommended for new endpoints):**
```csharp
// .RequireAuthorization() ensures valid JWT (any authenticated user)
// In-body capability check narrows to correct product role
app.MapPost("/api/referrals", async (...) => {
    var forbidden = await caps.ForbidIfLacksAsync(user, "referral:create", ct);
    if (forbidden is not null) return forbidden;
    // ...
}).RequireAuthorization();
```

**Pattern B — Custom policy (for shared middleware/pipeline checks):**
```csharp
// Define a policy that checks for a specific system role
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TenantAdminOnly", policy =>
        policy.RequireRole("TenantAdmin", "PlatformAdmin"));

    // Capability-based policies can be added here for Minimal API attribute use:
    options.AddPolicy("CanCreateReferral", policy =>
        policy.RequireClaim("product_roles", "CARECONNECT_REFERRER"));
});

// Usage on endpoint:
app.MapPost("/api/referrals", ...).RequireAuthorization("CanCreateReferral");
```

Pattern B is simpler but inflexible — it checks role codes in the JWT, not capabilities. It does not resolve through `RoleCapabilities`. Use only for coarse access gates on entire route groups.

### Transition Strategy

| Phase | What applies |
|---|---|
| **Now** | Legacy `[Authorize(Roles = ...)]` on admin endpoints. Capability checks added to new endpoints only. |
| **Phase 2** | New product endpoints exclusively use capability checks. Admin endpoints keep role attributes. |
| **Phase 3** | `UserRoles` deprecated. System roles (`PlatformAdmin`, `TenantAdmin`) migrated to `UserRoleAssignments`. Legacy attribute pattern still works via the same `ClaimTypes.Role` claim. |

---

## 8. Platform Admin Bypass

### The Pattern

Platform admins (`ClaimTypes.Role = "PlatformAdmin"`) should be able to access all endpoints without holding the specific product roles those endpoints require. This is implemented via a single property on `UserContext`:

```csharp
public bool IsPlatformAdmin =>
    SystemRoles.Contains("PlatformAdmin", StringComparer.OrdinalIgnoreCase);
```

All capability checks begin with:
```csharp
if (!user.IsPlatformAdmin)
{
    if (!await caps.HasCapabilityAsync(user.ProductRoles, "capability:code", ct))
        return Results.Forbid();
}
```

### Full Bypass Service Wrapper

For services that prefer to centralize the bypass:

```csharp
// Identity.Application/Services/AuthorizationService.cs
public sealed class AuthorizationService : IAuthorizationService
{
    private readonly ICapabilityService _caps;

    public AuthorizationService(ICapabilityService caps) => _caps = caps;

    public async Task<bool> IsAuthorizedAsync(
        UserContext user,
        string capabilityCode,
        CancellationToken ct = default)
    {
        // Platform admins bypass all capability checks
        if (user.IsPlatformAdmin) return true;
        return await _caps.HasCapabilityAsync(user.ProductRoles, capabilityCode, ct);
    }
}
```

Usage:
```csharp
if (!await _authz.IsAuthorizedAsync(user, "referral:create", ct))
    return Results.Forbid();
```

### Risks and Safeguards

| Risk | Safeguard |
|---|---|
| Any user claiming `PlatformAdmin` in a tampered token bypasses all checks | JWT signature (HMAC-SHA256) prevents claim tampering. Gateway validates signature on every request. |
| A compromised PlatformAdmin account has unlimited access | Enforce MFA for PlatformAdmin accounts (application-level, future phase). Rotate signing key on suspected compromise. |
| Service bug accidentally grants `IsPlatformAdmin = true` | `IsPlatformAdmin` checks `SystemRoles`, not product roles. A user cannot gain PlatformAdmin from `product_roles`. The two claim keys are distinct. |
| Bypass silently hides authorization bugs | In development, log all bypass events: `logger.LogWarning("PlatformAdmin bypass: {User} accessed {Capability}", user.Email, capabilityCode)`. |

---

## 9. Performance Considerations

### Login Query Chain

The login path has 3 sequential DB round-trips (tenant → user+password → user+roles+membership). This is acceptable because login is a low-frequency, high-value operation (happens once per session). Do not optimize prematurely. Target total login latency < 300ms on a co-located DB.

**If login latency exceeds threshold:**
- Combine queries 2 and 3 into a single query by starting from `Users` and including `UserRoles`, `UserOrganizationMemberships → Organization → OrganizationProducts → Product → ProductRoles` in one call
- Use `AsSplitQuery()` to break the multi-collection include into separate SQL queries (avoids Cartesian product on UserRoles × ProductRoles)

```csharp
// Optional split query pattern for login
var user = await _db.Users
    .AsSplitQuery()
    .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
    .Include(u => u.OrganizationMemberships.Where(m => m.IsActive))
        .ThenInclude(m => m.Organization)
            .ThenInclude(o => o.OrganizationProducts.Where(op => op.IsEnabled))
                .ThenInclude(op => op.Product)
                    .ThenInclude(p => p.ProductRoles.Where(pr => pr.IsActive))
    .Where(u => u.TenantId == tenantId && u.Email == email)
    .FirstOrDefaultAsync(ct);
```

### Capability Resolution Caching

The `CapabilityService` uses `IMemoryCache` with a 5-minute TTL and a composite key of sorted product role codes.

**Cache key examples:**
```
caps:CARECONNECT_REFERRER
caps:CARECONNECT_REFERRER|SYNQFUND_REFERRER
caps:SYNQLIEN_BUYER|SYNQLIEN_HOLDER
```

**Cache invalidation:** `ProductRoles` and `RoleCapabilities` are seed data changed only during deployments. The 5-minute TTL is sufficient — stale data will be gone well before the next deploy. No active invalidation is needed.

**For production scale:** If the service runs in multiple instances (horizontal scale), replace `IMemoryCache` with `IDistributedCache` (Redis). The interface is the same; only the DI registration changes:

```csharp
// Development
services.AddMemoryCache();
services.AddScoped<ICapabilityService, CapabilityService>();

// Production (with Redis)
services.AddStackExchangeRedisCache(o => o.Configuration = config["Redis:Connection"]);
services.AddScoped<ICapabilityService, DistributedCapabilityService>();
```

### Index Usage for Capability Query

The capability resolution query uses these indexes:

| Table | Index | Used for |
|---|---|---|
| `ProductRoles` | `IX_ProductRoles_Code` (unique) | WHERE `Code IN (...)` |
| `RoleCapabilities` | PK `(ProductRoleId, CapabilityId)` | JOIN on ProductRoleId |
| `RoleCapabilities` | `IX_RoleCapabilities_CapabilityId` | JOIN on CapabilityId |
| `Capabilities` | `IX_Capabilities_Code` (unique) | SELECT Code |

All joins use indexed columns. No table scans.

### N+1 Prevention Rules

| Anti-pattern | Fix |
|---|---|
| Calling `HasCapabilityAsync(...)` in a loop per item | Call `GetCapabilitiesAsync(...)` once, check the returned set in-memory per item |
| Loading `ProductRoles` per request outside login | Do not reload from DB; always read from JWT claims |
| Loading `UserOrganizationMemberships` on every request | Read from JWT `org_id`/`org_type` claims. Only load from DB when membership change detection is required |
| Loading all capabilities without caching | Always go through `CapabilityService` which respects the TTL cache |

---

## 10. Testing Strategy

### Unit Tests — JWT Generation

```csharp
// Tests/Identity.Tests/Services/JwtTokenServiceTests.cs
public class JwtTokenServiceTests
{
    private IConfiguration BuildConfig(int expiryMinutes = 60) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"]    = "test-signing-key-minimum-32-chars-long!!",
                ["Jwt:Issuer"]       = "test-issuer",
                ["Jwt:Audience"]     = "test-audience",
                ["Jwt:ExpiryMinutes"] = expiryMinutes.ToString()
            })
            .Build();

    [Fact]
    public void GenerateToken_ShouldIncludeAllBaseClaims()
    {
        var svc = new JwtTokenService(BuildConfig());
        var user   = TestFixtures.AdminUser();
        var tenant = TestFixtures.LegalSynqTenant();

        var (token, expiresAt) = svc.GenerateToken(user, tenant, ["PlatformAdmin"]);

        var handler  = new JwtSecurityTokenHandler();
        var jwt      = handler.ReadJwtToken(token);

        Assert.Equal(user.Id.ToString(),    jwt.Subject);
        Assert.Equal(user.Email,            jwt.Claims.First(c => c.Type == "email").Value);
        Assert.Equal(tenant.Id.ToString(),  jwt.Claims.First(c => c.Type == "tenant_id").Value);
        Assert.Equal(tenant.Code,           jwt.Claims.First(c => c.Type == "tenant_code").Value);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "PlatformAdmin");
    }

    [Fact]
    public void GenerateToken_WithOrg_ShouldIncludeOrgClaims()
    {
        var svc = new JwtTokenService(BuildConfig());
        var org = TestFixtures.LawFirmOrg();

        var (token, _) = svc.GenerateToken(
            TestFixtures.AdminUser(),
            TestFixtures.LegalSynqTenant(),
            [],
            org,
            ["CARECONNECT_REFERRER", "SYNQFUND_REFERRER"]);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(org.Id.ToString(), jwt.Claims.First(c => c.Type == "org_id").Value);
        Assert.Equal("LAW_FIRM",        jwt.Claims.First(c => c.Type == "org_type").Value);
        var productRoles = jwt.Claims.Where(c => c.Type == "product_roles")
                                     .Select(c => c.Value)
                                     .ToList();
        Assert.Contains("CARECONNECT_REFERRER", productRoles);
        Assert.Contains("SYNQFUND_REFERRER",    productRoles);
    }

    [Fact]
    public void GenerateToken_WithoutOrg_ShouldOmitOrgClaims()
    {
        var svc = new JwtTokenService(BuildConfig());
        var (token, _) = svc.GenerateToken(
            TestFixtures.AdminUser(), TestFixtures.LegalSynqTenant(), []);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.DoesNotContain(jwt.Claims, c => c.Type == "org_id");
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "org_type");
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "product_roles");
    }

    [Fact]
    public void GenerateToken_ShouldRespectExpiryMinutes()
    {
        var svc = new JwtTokenService(BuildConfig(expiryMinutes: 15));
        var (_, expiresAt) = svc.GenerateToken(
            TestFixtures.AdminUser(), TestFixtures.LegalSynqTenant(), []);

        var delta = expiresAt - DateTime.UtcNow;
        Assert.InRange(delta.TotalMinutes, 14, 16);
    }
}
```

### Unit Tests — Capability Resolution

```csharp
// Tests/Identity.Tests/Services/CapabilityServiceTests.cs
public class CapabilityServiceTests
{
    private IdentityDbContext BuildDbWithSeedData()
    {
        // Use EF Core in-memory provider with the capability seed
        var opts = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new IdentityDbContext(opts);

        var product = new Product { Id = Guid.NewGuid(), Code = "SYNQ_CARECONNECT", Name = "CareConnect", IsActive = true };
        var role    = new ProductRole { Id = Guid.NewGuid(), ProductId = product.Id, Code = "CARECONNECT_REFERRER", Name = "Referrer", IsActive = true, EligibleOrgType = "LAW_FIRM" };
        var cap1    = new Capability { Id = Guid.NewGuid(), ProductId = product.Id, Code = "referral:create",   Name = "Create Referral", IsActive = true };
        var cap2    = new Capability { Id = Guid.NewGuid(), ProductId = product.Id, Code = "referral:read:own", Name = "Read Own",        IsActive = true };

        db.Products.Add(product);
        db.ProductRoles.Add(role);
        db.Capabilities.AddRange(cap1, cap2);
        db.RoleCapabilities.AddRange(
            new RoleCapability { ProductRoleId = role.Id, CapabilityId = cap1.Id },
            new RoleCapability { ProductRoleId = role.Id, CapabilityId = cap2.Id });
        db.SaveChanges();

        return db;
    }

    [Fact]
    public async Task HasCapabilityAsync_GrantedCapability_ReturnsTrue()
    {
        var db  = BuildDbWithSeedData();
        var svc = new CapabilityService(db, new MemoryCache(new MemoryCacheOptions()));

        var result = await svc.HasCapabilityAsync(
            ["CARECONNECT_REFERRER"], "referral:create");

        Assert.True(result);
    }

    [Fact]
    public async Task HasCapabilityAsync_NonGrantedCapability_ReturnsFalse()
    {
        var db  = BuildDbWithSeedData();
        var svc = new CapabilityService(db, new MemoryCache(new MemoryCacheOptions()));

        var result = await svc.HasCapabilityAsync(
            ["CARECONNECT_REFERRER"], "referral:accept"); // RECEIVER cap

        Assert.False(result);
    }

    [Fact]
    public async Task HasCapabilityAsync_EmptyRoles_ReturnsFalse()
    {
        var db  = BuildDbWithSeedData();
        var svc = new CapabilityService(db, new MemoryCache(new MemoryCacheOptions()));

        var result = await svc.HasCapabilityAsync([], "referral:create");

        Assert.False(result);
    }

    [Fact]
    public async Task GetCapabilitiesAsync_CachesOnSecondCall()
    {
        var db    = BuildDbWithSeedData();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc   = new CapabilityService(db, cache);

        var first  = await svc.GetCapabilitiesAsync(["CARECONNECT_REFERRER"]);
        var second = await svc.GetCapabilitiesAsync(["CARECONNECT_REFERRER"]);

        // Both calls return the same capability set
        Assert.Equal(first, second);
        // Cache should have the entry (verify by checking count is unchanged after db.Dispose)
        db.Dispose();
        var third = await svc.GetCapabilitiesAsync(["CARECONNECT_REFERRER"]);
        Assert.Equal(first.Count, third.Count); // served from cache, no DB hit
    }
}
```

### Integration Tests — Endpoint Authorization

```csharp
// Tests/CareConnect.IntegrationTests/ReferralEndpointTests.cs
public class ReferralEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public ReferralEndpointTests(WebApplicationFactory<Program> f) => _factory = f;

    private HttpClient BuildClientWithClaims(params (string, string)[] claims)
    {
        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        "Test", opts => { })))
            .CreateClient();
        // Inject claims via test handler header
        foreach (var (k, v) in claims)
            client.DefaultRequestHeaders.Add($"X-Test-Claim-{k}", v);
        return client;
    }

    [Fact]
    public async Task CreateReferral_WithReferrerRole_Returns201()
    {
        var client = BuildClientWithClaims(
            ("product_roles", "CARECONNECT_REFERRER"),
            ("org_type", "LAW_FIRM"),
            ("org_id", Guid.NewGuid().ToString()),
            ("tenant_id", "20000000-0000-0000-0000-000000000001"));

        var response = await client.PostAsJsonAsync("/api/referrals",
            new { ProviderId = Guid.NewGuid(), Notes = "Test referral" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateReferral_WithFunderRole_Returns403()
    {
        var client = BuildClientWithClaims(
            ("product_roles", "SYNQFUND_FUNDER"), // wrong role
            ("org_type", "FUNDER"));

        var response = await client.PostAsJsonAsync("/api/referrals",
            new { ProviderId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateReferral_PlatformAdmin_Returns201()
    {
        var client = BuildClientWithClaims(
            (ClaimTypes.Role, "PlatformAdmin"));
            // no product_roles claim — admin bypasses capability check

        var response = await client.PostAsJsonAsync("/api/referrals",
            new { ProviderId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AcceptReferral_ReceiverFromDifferentOrg_Returns403()
    {
        var receiverOrgId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid(); // referral addressed to different org

        var client = BuildClientWithClaims(
            ("product_roles", "CARECONNECT_RECEIVER"),
            ("org_type", "PROVIDER"),
            ("org_id", receiverOrgId.ToString()));

        // Create referral addressed to differentOrgId...
        // (setup omitted for brevity)

        var response = await client.PostAsync($"/api/referrals/{differentOrgId}/accept", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

### Integration Tests — Cross-Tenant Access Restriction

```csharp
[Fact]
public async Task CannotAccessOtherTenantApplication()
{
    // User from tenant A tries to access application belonging to tenant B
    var tenantAUser = BuildClientWithClaims(
        ("tenant_id", TenantAId.ToString()),
        ("product_roles", "SYNQFUND_FUNDER"),
        ("org_id", FunderOrgInTenantA.ToString()));

    // applicationId belongs to tenant B
    var response = await tenantAUser.GetAsync($"/api/applications/{TenantBApplicationId}");

    // Service must return 403 or 404 (not the other tenant's data)
    Assert.True(
        response.StatusCode == HttpStatusCode.Forbidden ||
        response.StatusCode == HttpStatusCode.NotFound);
}
```

The service layer enforces this by always scoping queries to `TenantId` from the JWT claim:
```csharp
var app = await _db.Applications
    .Where(a => a.Id == id && a.TenantId == user.TenantId) // tenant scope enforced
    .FirstOrDefaultAsync(ct)
    ?? throw new NotFoundException();
```

Cross-tenant workflow access (a provider accepting a referral from a law firm on a different tenant) is handled by verifying `ReceiverOrgId` on the specific workflow record — not by relaxing the tenant scope query.
