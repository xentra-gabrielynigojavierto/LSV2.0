# Ingest Authentication

Platform Audit/Event Service — service-to-service authentication for `/internal/audit/*` endpoints.

---

## Overview

The ingest authentication layer protects all endpoints under `/internal/audit/` from
unauthorized submission. It is designed to be:

- **Mode-driven** — one config value (`IngestAuth:Mode`) selects the active strategy.
- **Extensible** — adding JWT, mTLS, or service-mesh auth requires implementing one interface and registering it in `Program.cs`.
- **Transparent to controllers** — `AuditEventIngestController` has zero knowledge of auth. The middleware enforces it before the controller is reached.
- **Consistent error format** — auth failures return the same `ApiResponse<T>` JSON envelope as controller errors.

---

## Protected Paths

| Path prefix | Protected |
|-------------|-----------|
| `/internal/audit/*` | ✅ Yes — all methods |
| `/api/auditevents/*` | ❌ No (legacy API) |
| `/health` | ❌ No |
| `/swagger/*` | ❌ No |

---

## Request Headers

| Header | Required | Description |
|--------|----------|-------------|
| `x-service-token` | Required (ServiceToken mode) | Shared secret credential. Case-insensitive per RFC 7230. |
| `x-source-system` | Optional | Logical name of the calling system (`"identity-service"`). Used for logging. Required when `AllowedSources` is configured or `RequireSourceSystemHeader = true`. |
| `x-source-service` | Optional | Sub-component of the caller (`"auth-worker"`). Logging only. |

### Example request

```http
POST /internal/audit/events HTTP/1.1
Host: audit-service.internal
Content-Type: application/json
x-service-token: <your-service-token>
x-source-system: identity-service
x-source-service: login-processor

{ ... event payload ... }
```

---

## Auth Modes

### `"None"` — Development pass-through

```json
"IngestAuth": {
  "Mode": "None"
}
```

- All requests accepted unconditionally.
- `ServiceAuthContext.ServiceName` is set to `"anonymous"`.
- A `WARNING` is logged at startup: `"ingest endpoints are unauthenticated"`.
- **Never deploy this mode to production.**

---

### `"ServiceToken"` — Shared secret per named service

```json
"IngestAuth": {
  "Mode": "ServiceToken",
  "ServiceTokens": [
    {
      "Token": "{{ injected from env }}",
      "ServiceName": "identity-service",
      "Description": "Identity microservice",
      "Enabled": true
    }
  ]
}
```

**Auth flow:**

1. Middleware reads `x-service-token` header → **401** if absent or empty.
2. Scans the `ServiceTokens` registry using constant-time comparison (`CryptographicOperations.FixedTimeEquals`).
3. Skips disabled entries (`Enabled: false`).
4. If no match → **401**.
5. If `AllowedSources` is configured → validates `x-source-system` → **403** if not in list.
6. Stores `ServiceAuthContext` in `HttpContext.Items` → calls `next()`.

**Constant-time scan:** The middleware always scans all registry entries regardless of match position to prevent timing-based side-channel attacks. Array lengths are normalized before `FixedTimeEquals` so token length cannot be inferred from response time.

**Token generation:**

```bash
openssl rand -base64 32
# → "wSdTK1y2mXG3vNq4uLo5pRj6sHe7fCa8bIk9dMn0="
```

**Environment variable injection (production):**

```bash
# First token (index 0)
IngestAuth__ServiceTokens__0__Token=<generated-token>
IngestAuth__ServiceTokens__0__ServiceName=identity-service
IngestAuth__ServiceTokens__0__Enabled=true

# Second token (index 1)
IngestAuth__ServiceTokens__1__Token=<generated-token>
IngestAuth__ServiceTokens__1__ServiceName=fund-service
IngestAuth__ServiceTokens__1__Enabled=true
```

---

### `"Bearer"` — JWT (planned, not yet implemented)

Future implementation will:

1. Extract the `Authorization: Bearer <token>` header.
2. Validate the JWT signature using `Microsoft.IdentityModel.Tokens`.
3. Enforce `RequiredClaims` and `RequiredRole` from `IngestAuthOptions`.
4. Map the `sub` or `service_name` claim to `ServiceAuthContext.ServiceName`.

To add this mode: implement `IIngestAuthenticator` as `JwtIngestAuthenticator` and register it in `Program.cs`.

---

### `"MtlsHeader"` — mTLS via proxy forwarding (planned)

Future implementation will:

1. Read the client certificate from a forwarded header (e.g. `X-Forwarded-Client-Cert`).
2. Validate the certificate fingerprint or CN against a registry.
3. Map the certificate subject to `ServiceAuthContext.ServiceName`.

Suitable for Istio/Envoy/Nginx deployments that terminate mTLS and forward cert metadata as headers.

---

### `"MeshInternal"` — Service mesh trust-on-network (planned)

For zero-trust mesh environments (Istio, Linkerd):

1. The mesh sidecar injects a SPIFFE identity into the request.
2. The middleware reads the SPIFFE identity header and validates it against an allowlist.
3. No shared secrets required.

---

## Source Allowlist

When `AllowedSources` is non-empty, the `x-source-system` header value must match one of the listed identifiers — even if the token is valid.

```json
"IngestAuth": {
  "Mode": "ServiceToken",
  "AllowedSources": ["identity-service", "fund-service", "care-connect-api"]
}
```

Case-insensitive comparison. Empty list = allow any source.

On mismatch: **403 Forbidden** with body:
```json
{
  "success": false,
  "message": "Source system 'unknown-system' is not in the configured allowlist.",
  "traceId": "...",
  "data": null,
  "errors": []
}
```

---

## Auth Flow Diagram

```
Inbound request
      │
      ▼
IngestAuthMiddleware.InvokeAsync()
      │
      ├─ Path does NOT start with /internal/audit → skip (next())
      │
      ├─ Mode = "None" → SetAnonymousContext → next()
      │
      └─ Delegate to IIngestAuthenticator.AuthenticateAsync(headers)
             │
             ├─ x-service-token absent/empty → 401 MissingToken
             │
             ├─ ServiceTokens registry empty → 401 TokenNotConfigured
             │
             ├─ No registry entry matches → 401 InvalidToken
             │
             └─ Match found
                    │
                    ├─ AllowedSources configured AND x-source-system not in list → 403 Forbidden
                    │
                    └─ Set ServiceAuthContext in HttpContext.Items → next()
                              │
                              ▼
                    AuditEventIngestController
                    (reads ServiceAuthContext if needed)
```

---

## Status Code Reference

| Code | Trigger | Body message |
|------|---------|--------------|
| **401 Unauthorized** | `x-service-token` header absent or empty | `"Missing required header: x-service-token."` |
| **401 Unauthorized** | Token doesn't match any enabled registry entry | `"Authentication failed. Verify your service token..."` |
| **401 Unauthorized** | No tokens configured in registry | `"Authentication failed. Verify your service token..."` |
| **403 Forbidden** | Token valid but `x-source-system` not in `AllowedSources` | `"Source system '...' is not in the configured allowlist."` |
| **403 Forbidden** | `x-source-system` missing when `AllowedSources` configured | `"Header x-source-system is required..."` |

All error responses use the `ApiResponse<T>` envelope shape:
```json
{ "success": false, "message": "...", "traceId": "...", "data": null, "errors": [] }
```

---

## ServiceAuthContext

After successful authentication, `IngestAuthMiddleware` stores a `ServiceAuthContext` in `HttpContext.Items[ServiceAuthContext.ItemKey]`. Controllers can read it:

```csharp
var ctx = HttpContext.Items[ServiceAuthContext.ItemKey] as ServiceAuthContext;
// ctx.ServiceName   → "identity-service"
// ctx.SourceSystem  → "identity-service" (from x-source-system header)
// ctx.SourceService → "login-processor" (from x-source-service header)
// ctx.AuthMode      → "ServiceToken"
```

This is useful for including the authenticated caller's identity in log messages or persisted audit metadata.

---

## Startup Logging

| Condition | Level | Message |
|-----------|-------|---------|
| Mode = "None" | WARNING | `"ingest endpoints are unauthenticated. Set Mode=ServiceToken..."` |
| Mode = "ServiceToken", no tokens configured | WARNING | `"No enabled token entries configured. All requests will be rejected."` |
| Mode = "ServiceToken", tokens loaded | INFO | `"ServiceToken auth initialized — N registered service(s): ..."` |

---

## Extension Guide — Adding a New Auth Mode

1. **Create the authenticator:**

```csharp
// Services/JwtIngestAuthenticator.cs
public sealed class JwtIngestAuthenticator : IIngestAuthenticator
{
    public string Mode => "Bearer";

    public Task<AuthResult> AuthenticateAsync(IHeaderDictionary headers, CancellationToken ct = default)
    {
        // Extract Authorization: Bearer <token>
        // Validate JWT signature, expiry, claims
        // Return AuthResult(Succeeded: true, ServiceName: sub_claim)
    }
}
```

2. **Register in Program.cs:**

```csharp
builder.Services.AddSingleton<JwtIngestAuthenticator>();

builder.Services.AddSingleton<IIngestAuthenticator>(sp => ingestAuthMode switch
{
    "ServiceToken" => sp.GetRequiredService<ServiceTokenAuthenticator>(),
    "Bearer"       => sp.GetRequiredService<JwtIngestAuthenticator>(),   // ← add this
    _              => sp.GetRequiredService<NullIngestAuthenticator>(),
});
```

3. **Configure:**

```json
"IngestAuth": {
  "Mode": "Bearer",
  "RequiredClaims": ["service_name"],
  "RequiredRole": "platform-audit-ingest"
}
```

No changes to `IngestAuthMiddleware`, controllers, or validators are needed.

---

## Security Recommendations

| Recommendation | Rationale |
|---------------|-----------|
| Use `Mode = "ServiceToken"` in staging and production | None mode is dev-only |
| Generate tokens with `openssl rand -base64 32` | 256 bits entropy; sufficient for HMAC-based secrets |
| Inject tokens via environment variables, never commit them | Prevents secret leakage in git history |
| Rotate tokens on a scheduled cadence (e.g. 90 days) | Limits blast radius of token compromise |
| Use overlapping validity windows during rotation | Prevents downtime: add new entry → deploy → remove old entry |
| Enable `RequireSourceSystemHeader` in production | Enforces provenance — makes log analysis easier |
| Configure `AllowedSources` in production | Defense-in-depth: valid token + known source = authorized |
| Monitor startup warnings | `"TokenNotConfigured"` at startup means no requests will be accepted |
