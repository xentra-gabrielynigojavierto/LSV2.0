# Documents Service — Swagger / OpenAPI Setup Guide

This guide explains how to serve the OpenAPI specification and a Swagger UI inside the Docs Service Express application.

---

## Overview

Two things need to be served:

| What | Route | Purpose |
|------|-------|---------|
| Raw OpenAPI spec | `GET /openapi.json` | Machine-readable; consumed by tools, gateways, and codegen |
| Swagger UI | `GET /docs` | Human-readable, interactive documentation UI |

---

## Required packages

```bash
cd apps/services/docs
npm install swagger-ui-express
npm install --save-dev @types/swagger-ui-express
```

To load the YAML spec file at startup:

```bash
npm install js-yaml
npm install --save-dev @types/js-yaml
```

---

## Implementation

### Step 1 — Copy the spec into the service directory

The spec was generated at `analysis/openapi-documents.yaml`. Copy it to the service root so it is available at runtime:

```bash
cp apps/services/docs/analysis/openapi-documents.yaml apps/services/docs/openapi.yaml
```

Or reference it by path in the source code (see Step 2).

---

### Step 2 — Create the docs router

Create `src/api/routes/docs.ts`:

```typescript
import { Router }     from 'express';
import swaggerUi      from 'swagger-ui-express';
import yaml           from 'js-yaml';
import fs             from 'fs';
import path           from 'path';
import { config }     from '@/shared/config';

const router = Router();

// Load spec once at startup — fail fast if the file is missing
const specPath = path.resolve(__dirname, '../../../openapi.yaml');
const specYaml = fs.readFileSync(specPath, 'utf8');
const spec     = yaml.load(specYaml) as Record<string, unknown>;

// Serve raw spec
router.get('/openapi.json', (_req, res) => {
  res.json(spec);
});

router.get('/openapi.yaml', (_req, res) => {
  res.setHeader('Content-Type', 'text/yaml');
  res.send(specYaml);
});

// Serve Swagger UI — only in non-production environments
if (config.NODE_ENV !== 'production') {
  router.use(
    '/',
    swaggerUi.serve,
    swaggerUi.setup(spec, {
      customSiteTitle: 'Documents Service API',
      swaggerOptions: {
        persistAuthorization: true,   // keeps the Bearer token between page refreshes
        displayRequestDuration: true,
        tryItOutEnabled: true,
      },
    }),
  );
}

export default router;
```

> **Note:** The `if (config.NODE_ENV !== 'production')` guard keeps Swagger UI off in production while still serving the raw JSON/YAML spec (which API gateways and codegen tools need). Remove the guard if you want UI in production too.

---

### Step 3 — Mount the router in app.ts

Open `src/app.ts` and add the import and `app.use()` call:

```typescript
// existing imports …
import docsRouter from '@/api/routes/docs';

// inside createApp(), alongside the other route registrations:
app.use('/docs', docsRouter);
```

After this change the following routes will be active:

| Route | Content |
|-------|---------|
| `GET /docs` | Swagger UI (HTML) |
| `GET /docs/openapi.json` | OpenAPI spec as JSON |
| `GET /docs/openapi.yaml` | OpenAPI spec as YAML |

---

### Step 4 — Test locally

```bash
# Start the service
npm run dev

# Verify the spec loads
curl http://localhost:5005/docs/openapi.json | jq '.info.title'
# → "Documents Service API"

# Open Swagger UI in a browser
open http://localhost:5005/docs
```

---

## Using Swagger UI to test authenticated endpoints

1. Open `http://localhost:5005/docs`
2. Click **Authorize** (padlock icon in the top-right)
3. In the **BearerAuth** field, paste your JWT (without the `Bearer ` prefix)
4. Click **Authorize**, then **Close**
5. All authenticated requests will include the `Authorization: Bearer <jwt>` header

---

## Serving via the API Gateway

If the platform API Gateway proxies to the Docs Service at `/docs/*`, the Swagger UI will be accessible at the gateway URL. No additional configuration is required on the service side — the gateway simply forwards the request.

Example (if gateway maps `/api/docs-service/*` → `http://docs-service:5005/*`):

```
https://api.legalsynq.com/api/docs-service/docs       → Swagger UI
https://api.legalsynq.com/api/docs-service/docs/openapi.json → Spec
```

---

## Production considerations

| Concern | Recommendation |
|---------|---------------|
| Swagger UI in production | Disable (guard with `NODE_ENV !== 'production'`) |
| OpenAPI JSON/YAML in production | Enable — needed by API gateways, monitoring, and codegen |
| Authentication on /docs | Consider adding `requireAuth` middleware to the `/docs` router in production if the spec should not be publicly readable |
| Keeping the spec in sync | Run the spec generation step as part of CI/CD to ensure the YAML stays in sync with the implementation |

---

## Alternative: Inline spec (no YAML file)

If you prefer to keep the spec as a JavaScript object rather than a separate file, you can export it from a TypeScript file:

```typescript
// src/api/docs/spec.ts
export const openApiSpec = {
  openapi: '3.1.0',
  info: { title: 'Documents Service API', version: '1.0.0' },
  // ... full spec object
};
```

Then import it directly in `docs.ts` without `js-yaml` or `fs`. This has the advantage of TypeScript type checking but makes the spec harder to consume by external tools that expect a standalone YAML/JSON file.

---

## Summary of packages

| Package | Purpose | Install command |
|---------|---------|----------------|
| `swagger-ui-express` | Serves Swagger UI in Express | `npm install swagger-ui-express` |
| `@types/swagger-ui-express` | TypeScript types | `npm install -D @types/swagger-ui-express` |
| `js-yaml` | Parse YAML spec file at startup | `npm install js-yaml` |
| `@types/js-yaml` | TypeScript types | `npm install -D @types/js-yaml` |
