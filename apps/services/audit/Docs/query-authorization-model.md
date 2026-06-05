# Query Authorization Model

**Service**: Platform Audit/Event Service  
**Scope**: `/audit/*` query and retrieval endpoints  
**Version**: Step 14

---

## Overview

The query authorization layer controls who can read audit event records and which records they can see. It is deliberately provider-neutral — it does not hardcode any JWT issuer, OIDC provider, or claims schema. All identity-provider-specific details live in configuration.

The layer consists of four collaborating components:

| Component | Responsibility |
|---|---|
| `QueryAuthOptions` | Configuration binding — role lists, claim type names, enforcement flags |
| `IQueryCallerResolver` | Translates the raw HTTP request into an `IQueryCallerContext` |
| `QueryAuthMiddleware` | Resolves caller per-request; enforces the 401 gate |
| `IQueryAuthorizer` | Validates scope; applies constraint overrides to the query |

---

## Scope Model

Six conceptual scopes map to real organizational security boundaries:

| Scope | Numeric | Cross-tenant | Own-tenant access | Visibility floor |
|---|---|---|---|---|
| `Unknown` | 0 | Denied | Denied | — |
| `UserSelf` | 1 | Denied | Own records only (`actorId` = `UserId`) | `User` |
| `TenantUser` | 2 | Denied | `User`-scope records in own tenant | `User` |
| `Restricted` | 3 | Denied | `Tenant`-scope and below, read-only | `Tenant` |
| `OrganizationAdmin` | 4 | Denied | `Organization`-scope and below in own org | `Organization` |
| `TenantAdmin` | 5 | Denied | All records within own tenant | `Tenant` |
| `PlatformAdmin` | 6 | Allowed | All records (no tenant restriction) | `Platform` |

**Visibility floor** is the *minimum* `VisibilityScope` value the caller may see. The floor maps to a `>=` filter against the record's stored `VisibilityScope` value. Because `VisibilityScope` is ordered as `Platform(1) < Tenant(2) < Organization(3) < User(4) < Internal(5)`, a higher floor value means a *more restrictive* access window.

`Internal`-scope records are never returned regardless of caller scope — they are excluded by the repository layer.

---

## Visibility Scope Reference

| `VisibilityScope` | Value | What each scope can see |
|---|---|---|
| `Platform` | 1 | `PlatformAdmin` only |
| `Tenant` | 2 | `TenantAdmin`, `Restricted`, `PlatformAdmin` |
| `Organization` | 3 | Above + `OrganizationAdmin` |
| `User` | 4 | All authenticated scopes |
| `Internal` | 5 | Never returned via the query API |

---

## Auth Modes

Set via `QueryAuth:Mode` in appsettings or `QueryAuth__Mode` environment variable.

### `None` (development / test only)

All callers are resolved to `PlatformAdmin` scope without any credentials.  
All queries are unrestricted.  
**Never use in production.**

### `Bearer` (JWT / OIDC)

The upstream JWT middleware (not this service) validates the token signature and sets `HttpContext.User` with claims. This service reads claims by name (configured in `QueryAuth`) to determine the caller's scope.

No identity provider is hardcoded. The claim type names are fully configurable:

| Setting | Default | Purpose |
|---|---|---|
| `TenantIdClaimType` | `tenant_id` | Claim containing the caller's tenant identifier |
| `OrganizationIdClaimType` | `org_id` | Claim containing the caller's organization identifier |
| `UserIdClaimType` | `sub` | Claim containing the caller's stable user identifier |
| `RoleClaimType` | `role` | Claim containing the caller's roles |

---

## Role → Scope Mapping

Scope is resolved by checking the caller's roles against configured lists. **First match wins**, checked from highest to lowest privilege:

| Config key | Default roles | Grants scope |
|---|---|---|
| `PlatformAdminRoles` | `platform-audit-admin` | `PlatformAdmin` |
| `TenantAdminRoles` | `tenant-admin`, `compliance-officer` | `TenantAdmin` |
| `OrganizationAdminRoles` | `org-admin`, `department-admin` | `OrganizationAdmin` |
| `RestrictedRoles` | `compliance-reader`, `auditor` | `Restricted` |
| `UserSelfRoles` | `self-reader` | `UserSelf` |
| `TenantUserRoles` | `tenant-user`, `user` | `TenantUser` |

If the caller is authenticated but no role matches any list, they receive `TenantUser` as a safe fallback (minimum authenticated scope).

---

## Enforcement Behavior

Authorization is performed in two phases every request:

### Phase 1 — Access check

| Condition | Outcome |
|---|---|
| `Scope = Unknown` and not authenticated | 401 Unauthenticated |
| `Scope = Unknown` and authenticated | 403 Forbidden (scope not resolvable) |
| `Scope = UserSelf` but no `UserId` claim | 403 Forbidden |
| Non-`PlatformAdmin`, `query.TenantId` ≠ `caller.TenantId` | 403 Forbidden (cross-tenant attempt) |
| `EnforceTenantScope = true`, non-`PlatformAdmin`, no `TenantId` claim | 403 Forbidden |

### Phase 2 — Constraint application (query mutation)

After Phase 1 passes, the query object is mutated in-place before it reaches the repository:

| Caller scope | Constraint applied |
|---|---|
| All except `PlatformAdmin` | `query.TenantId` = `caller.TenantId` (claim wins over request value) |
| `OrganizationAdmin` | `query.OrganizationId` = `caller.OrganizationId` |
| `UserSelf` | `query.ActorId` = `caller.UserId` |
| All scopes | `query.MaxVisibility` = `max(scope_floor, existing_value)` — more restrictive wins |

This design ensures that even if a caller sends a query with mismatched `tenantId` or another user's `actorId`, the constraints are silently corrected rather than leaked. The repository executes only the constrained query.

---

## Multi-tenancy Isolation Guarantee

Non-`PlatformAdmin` callers **cannot access other tenants' data** even if they construct a request with a different `tenantId`. The `QueryAuthorizer` unconditionally overrides `query.TenantId` with `caller.TenantId` before execution.

Tenant isolation is enforced independently of whether the client sends a `tenantId` in the query string.

---

## Integrity Hash Exposure

The `ExposeIntegrityHash` flag controls whether the `hash` field appears in query responses.

- `false` (default): hash is always `null` in responses, regardless of caller scope.
- `true`: hash is included for `PlatformAdmin` callers; other scopes still receive `null`.

This field should be `false` in production and `true` only in development or for dedicated integrity-verification tooling.

---

## Integration Path — Adding a New Identity Provider

No changes to middleware, authorizer, or controller code are needed to switch or add identity providers.

**Steps:**

1. Register the upstream JWT middleware for your provider (e.g. `AddJwtBearer()` for Entra ID, Auth0, Keycloak).
2. In `appsettings.Production.json`, set:
   - `QueryAuth:Mode = "Bearer"`
   - `QueryAuth:TenantIdClaimType` to the claim name your provider uses for tenant ID.
   - `QueryAuth:RoleClaimType` to the claim name your provider uses for roles.
   - Add your actual role names to the appropriate role lists.
3. Ensure your JWT middleware sets `HttpContext.User` with the resolved claims principal — this is standard `AddJwtBearer()` behavior.

**Custom resolver (advanced):**  
If your provider uses a non-standard format (e.g. Keycloak's nested `realm_access.roles` claim, mTLS certificate attributes, or API keys), implement `IQueryCallerResolver` and register it in `Program.cs`:

```csharp
builder.Services.AddSingleton<IQueryCallerResolver>(sp =>
    cfg["QueryAuth:Mode"] switch
    {
        "Bearer"     => sp.GetRequiredService<ClaimsCallerResolver>(),
        "KeycloakJwt" => sp.GetRequiredService<KeycloakCallerResolver>(),
        "ApiKey"     => sp.GetRequiredService<ApiKeyCallerResolver>(),
        _            => sp.GetRequiredService<AnonymousCallerResolver>(),
    });
```

The middleware, authorizer, and all controllers require no changes.

---

## Files

| File | Purpose |
|---|---|
| `Authorization/CallerScope.cs` | Conceptual scope enum |
| `Authorization/IQueryCallerContext.cs` | Per-request caller identity interface |
| `Authorization/QueryCallerContext.cs` | Concrete implementation + factory helpers |
| `Authorization/IQueryCallerResolver.cs` | Resolver contract |
| `Authorization/AnonymousCallerResolver.cs` | Dev-mode resolver (Mode=None) |
| `Authorization/ClaimsCallerResolver.cs` | Bearer/claims resolver |
| `Authorization/QueryAuthorizationResult.cs` | Authorization decision carrier |
| `Authorization/IQueryAuthorizer.cs` | Authorizer contract |
| `Authorization/QueryAuthorizer.cs` | Enforcement implementation |
| `Middleware/QueryAuthMiddleware.cs` | Per-request context resolution + 401 gate |
| `Configuration/QueryAuthOptions.cs` | All authorization configuration |
