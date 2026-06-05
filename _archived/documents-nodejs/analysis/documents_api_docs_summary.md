# Documents Service — API Documentation Summary

---

## Files Created

| File | Description | Size |
|------|-------------|------|
| `analysis/documents_api_documentation.md` | Full human-readable API reference | ~450 lines |
| `analysis/openapi-documents.yaml` | OpenAPI 3.1.0 spec — machine-readable | ~570 lines |
| `analysis/documents_swagger_setup.md` | Step-by-step Swagger UI integration guide | ~130 lines |
| `analysis/documents_api_docs_summary.md` | This file | — |

---

## Endpoints Documented

| Method | Path | Auth | Rate Limit Profile |
|--------|------|------|--------------------|
| `GET` | `/health` | None | None |
| `GET` | `/health/ready` | None | None |
| `POST` | `/documents` | JWT | Upload (10/min) |
| `GET` | `/documents` | JWT | General (100/min) |
| `GET` | `/documents/:id` | JWT | General |
| `PATCH` | `/documents/:id` | JWT | General |
| `DELETE` | `/documents/:id` | JWT | General |
| `POST` | `/documents/:id/versions` | JWT | Upload (10/min) |
| `GET` | `/documents/:id/versions` | JWT | General |
| `POST` | `/documents/:id/view-url` | JWT | Signed URL (30/min) |
| `POST` | `/documents/:id/download-url` | JWT | Signed URL (30/min) |
| `GET` | `/documents/:id/content` | JWT | General |
| `GET` | `/access/:token` | None (opaque token) | None |

**Total: 13 endpoints** across 4 route files (`health.ts`, `documents.ts`, `access.ts` + internal file serving).

---

## Source Files Inspected

| File | What was read |
|------|--------------|
| `src/api/routes/documents.ts` | All 11 document endpoints, validators, middleware chain |
| `src/api/routes/access.ts` | Token redemption endpoint, token format validation |
| `src/api/routes/health.ts` | Liveness and readiness check implementations |
| `src/app.ts` | Route mounting, CORS config, middleware order, security headers |
| `src/api/middleware/auth.ts` | JWT extraction, Bearer format, principal extraction |
| `src/api/middleware/error-handler.ts` | Error type hierarchy, HTTP status mapping |
| `src/api/middleware/file-validator.ts` | MIME whitelist, magic-byte check, size limit |
| `src/application/document-service.ts` | Business logic, scan gating, legal hold, tenant resolution |
| `src/application/access-token-service.ts` | Token issuance, redemption, one-time-use enforcement |
| `src/application/rbac.ts` | Role permission matrix, assertPermission, assertTenantScope |
| `src/application/tenant-guard.ts` | assertDocumentTenantScope, resolveEffectiveTenantId |
| `src/infrastructure/database/document-repository.ts` | Query shapes, filter options, pagination |
| `src/shared/errors.ts` | All error classes, codes, HTTP status codes |
| `src/shared/config.ts` | Environment variable defaults (TTL, file size, providers) |
| `src/shared/logger.ts` | Pino redaction paths (confirmed no token/auth in logs) |

---

## Assumptions Made

| Assumption | Basis |
|------------|-------|
| Service port is `5005` | `config.PORT` default in `config.ts` |
| Token TTL default is 300 seconds (5 minutes) | `ACCESS_TOKEN_TTL_SECONDS` default |
| Redirect (presigned) URL is valid for 30 seconds | Hardcoded in `AccessTokenService.redeem` |
| Max file size is 50 MB | `MAX_FILE_SIZE_MB` default |
| `scanThreats` is always `[]` in normal operation | Only `threatCount` is guaranteed non-empty for infected files; threat names may or may not be strings |
| `view-url` and `download-url` differ only in the `type` field returned | Confirmed by route code — both call the same service method with a different `type` argument |
| `GET /documents/:id/content` queries the `currentVersionId` version's storage key | Inferred from service code; specific version targeting via query param is not implemented |
| The `redeemUrl` in the issued token response is a relative path | Confirmed from `AccessTokenService.issue` — it returns `/access/<token>` |

---

## Gaps and Ambiguities Found in Code

| Gap | Location | Detail |
|-----|----------|--------|
| No rate limiter on `/access/:token` | `access.ts` | Endpoint is unauthenticated and has no limiter applied — documented as a known security gap in the architecture review |
| `retainUntil` is stored but not enforced | `document-service.ts` | The field is accepted in PATCH and returned in GET, but `delete()` does not check it before soft-deleting |
| Legal hold uses two separate fields | `document-service.ts` | `legalHoldAt` (timestamp) blocks deletion; `status = 'LEGAL_HOLD'` is a separate field. Setting status alone does not set `legalHoldAt`, so delete guard may not fire |
| `GET /documents/:id/content` — version targeting | `documents.ts` | The `?type=` query param exists; whether a `?versionId=` param is supported is not confirmed — not observed in the route code |
| Document type validation | `documents.ts` | `documentTypeId` is validated as a UUID format but is NOT validated against the `document_types` table at runtime — a non-existent UUID will be stored without error |
| `scanThreats` content | Domain model | The array type is `string[]` but whether threat names are always plain strings (vs structured objects) depends on the ClamAV provider and is not guaranteed by the interface |
| CORS origins in response | `app.ts` | `CORS_ORIGINS` is validated at runtime; if not set, `*` is the default — this should be verified for the deployment environment |
| `PlatformAdmin`-only test header | `documents.ts` | `X-Admin-Target-Tenant` is extracted from the request for all callers but silently ignored for non-admins — documented but not in the route's header schema |

---

## Recommended Next Steps to Publish the Docs

### Minimum (internal use)

1. **Install packages** in the Docs Service:
   ```bash
   cd apps/services/docs
   npm install swagger-ui-express js-yaml
   npm install -D @types/swagger-ui-express @types/js-yaml
   ```

2. **Copy the spec** to the service root:
   ```bash
   cp analysis/openapi-documents.yaml openapi.yaml
   ```

3. **Create `src/api/routes/docs.ts`** following the guide in `documents_swagger_setup.md`.

4. **Mount the router** in `src/app.ts`:
   ```typescript
   import docsRouter from '@/api/routes/docs';
   app.use('/docs', docsRouter);
   ```

5. **Restart the service** and verify:
   - `http://localhost:5005/docs` → Swagger UI
   - `http://localhost:5005/docs/openapi.json` → JSON spec

### For Production / CareConnect Integration

6. **Validate spec** against the running service using a tool like `dredd` or Postman collection import.

7. **Automate spec validation** in CI: add a step that loads `openapi.yaml` and runs `openapi-validator` or equivalent to catch drift between the spec and implementation.

8. **Share the spec with CareConnect frontend team** — they can import `openapi.yaml` into Postman or use `openapi-typescript` / `openapi-generator` to generate a typed client.

9. **Consider API Gateway integration** — many gateways (Kong, AWS API Gateway) can import the OpenAPI spec directly to configure routing and auth validation.

---

## Error Codes Quick Reference

| Code | HTTP | When |
|------|------|------|
| `VALIDATION_ERROR` | 400 | Body/field schema failure |
| `FILE_VALIDATION_ERROR` | 400 | Empty file or magic-byte mismatch |
| `AUTHENTICATION_REQUIRED` | 401 | Missing/invalid/expired JWT |
| `TOKEN_EXPIRED` | 401 | Access token not found or expired |
| `TOKEN_INVALID` | 401 | Token format wrong or already used |
| `ACCESS_DENIED` | 403 | RBAC, tenant mismatch, or legal hold |
| `SCAN_BLOCKED` | 403 | File access blocked by scan status |
| `NOT_FOUND` | 404 | Resource absent or other tenant |
| `FILE_TOO_LARGE` | 413 | File exceeds MAX_FILE_SIZE_MB |
| `UNSUPPORTED_FILE_TYPE` | 422 | MIME not in allowed list |
| `INFECTED_FILE` | 422 | Malware detected at upload |
| `RATE_LIMIT_EXCEEDED` | 429 | Any rate limit bucket exceeded |
| `REDIS_UNAVAILABLE` | 503 | Redis backend unreachable |
| `INTERNAL_SERVER_ERROR` | 500 | Unhandled error |
