# Notifications Service API Documentation

## Table of Contents

- [Overview](#overview)
- [Authentication](#authentication)
- [Tenant Context](#tenant-context)
- [Common Models](#common-models)
- [Error Responses](#error-responses)
- [Notifications](#notifications-endpoints)
- [Templates](#templates-endpoints)
- [Global Templates](#global-templates-endpoints)
- [Providers](#providers-endpoints)
- [Webhooks](#webhooks-endpoints)
- [Contacts](#contacts-endpoints)
- [Branding](#branding-endpoints)
- [Billing](#billing-endpoints)
- [Internal](#internal-endpoints)

---

## Overview

Base URL prefix: `/v1`

All endpoints in the Notifications service are JSON-based. Request and response bodies use `application/json` content type unless otherwise noted.

---

## Authentication

The Notifications service uses different authentication mechanisms depending on the endpoint category:

### Tenant API Endpoints (`/v1/*`)

Tenant-facing endpoints require the `X-Tenant-Id` header for tenant identification (see [Tenant Context](#tenant-context)). No additional bearer token or API key authentication is enforced at the middleware level.

### Webhook Endpoints (`/v1/webhooks/*`)

Webhook endpoints are authenticated via provider-specific signature verification:

- **SendGrid:** Validated using SendGrid's event webhook signature headers.
- **Twilio:** Validated using Twilio's request signature and the reconstructed request URL.

If signature verification fails, the endpoint returns `401 Unauthorized`.

### Internal Endpoints (`/internal/*`)

Internal service-to-service endpoints are protected by a shared token:

| Header | Type | Required | Description |
|---|---|---|---|
| `X-Internal-Service-Token` | `string` | Yes | Shared secret token for service-to-service authentication |

| Status | Condition |
|---|---|
| `401 Unauthorized` | Token is missing or does not match the configured `INTERNAL_SERVICE_TOKEN` |
| `503 Service Unavailable` | `INTERNAL_SERVICE_TOKEN` is not configured (non-development environments only) |

In development mode, if `INTERNAL_SERVICE_TOKEN` is not configured, requests are allowed without a token.

---

## Tenant Context

Most endpoints require a tenant context provided via the `X-Tenant-Id` HTTP header.

| Header | Type | Required | Description |
|---|---|---|---|
| `X-Tenant-Id` | `guid` | Yes | Tenant unique identifier |

The `TenantMiddleware` reads this header and makes it available to all downstream endpoint handlers. If the header is missing or not a valid GUID, a `400 Bad Request` is returned:

```json
{
  "error": "Missing or invalid X-Tenant-Id header"
}
```

**Exemptions:** The following paths are exempt from tenant resolution and do not require the `X-Tenant-Id` header:

| Path Prefix | Reason |
|---|---|
| `/health` | Health check |
| `/info` | Service info |
| `/v1/webhooks` | Webhook receivers (provider-authenticated) |
| `/internal` | Internal service-to-service calls |

---

## Common Models

### List Response

List endpoints return an array of items directly (not wrapped in a paginated envelope). Pagination is controlled via `limit` and `offset` query parameters where supported.

| Parameter | Type | Default | Description |
|---|---|---|---|
| `limit` | `integer` | `50` | Maximum number of items to return |
| `offset` | `integer` | `0` | Number of items to skip |

---

## Error Responses

### 400 Bad Request

Returned when the `X-Tenant-Id` header is missing or invalid.

```json
{
  "error": "Missing or invalid X-Tenant-Id header"
}
```

### 404 Not Found

Returned when a requested resource does not exist or does not belong to the calling tenant.

### 422 Unprocessable Entity

Returned when a notification submission is blocked by policy.

### Common Status Codes

| Status | Condition |
|---|---|
| `400 Bad Request` | Missing or invalid `X-Tenant-Id` header |
| `404 Not Found` | Resource not found or not accessible by the current tenant |
| `422 Unprocessable Entity` | Notification blocked by policy (notification submission only) |

**Per-endpoint status code summary:**

| Endpoint Type | Success | Possible Errors |
|---|---|---|
| List (`GET` returning arrays) | `200 OK` | `400` |
| Get by ID (`GET` returning single item) | `200 OK` | `400`, `404` |
| Create (`POST`) | `201 Created` | `400` |
| Update (`PUT`) | `200 OK` | `400`, `404` |
| Delete (`DELETE`) | `204 No Content` | `400`, `404` |
| Submit notification (`POST`) | `201 Created` | `400`, `422` |

---

## Notifications Endpoints

Base path: `/v1/notifications`

### POST `/v1/notifications/`

Submit a notification for delivery.

**Headers:** `X-Tenant-Id` required.

**Request Body: `SubmitNotificationDto`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `channel` | `string` | Yes | No | Delivery channel (e.g., `email`, `sms`) |
| `recipient` | `object` | Yes | No | Recipient details (channel-specific structure) |
| `message` | `object` | Yes | No | Message content (channel-specific structure) |
| `metadata` | `object` | No | Yes | Arbitrary metadata attached to the notification |
| `idempotencyKey` | `string` | No | Yes | Idempotency key to prevent duplicate sends |
| `templateKey` | `string` | No | Yes | Template key to use for rendering |
| `templateData` | `map<string, string>` | No | Yes | Key-value pairs for template variable substitution |
| `productType` | `string` | No | Yes | Product type for branding resolution |
| `brandedRendering` | `boolean` | No | Yes | Whether to apply branded rendering |
| `overrideSuppression` | `boolean` | No | Yes | Whether to override suppression rules |
| `overrideReason` | `string` | No | Yes | Reason for overriding suppression |

**Response:** `201 Created` — `NotificationResultDto`

Returns the submission result with a `Location` header pointing to `/v1/notifications/{id}`.

**Error:** `422 Unprocessable Entity` — if the notification is blocked by policy (e.g., suppression).

---

### GET `/v1/notifications/{id}`

Get a notification by its unique identifier.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Notification unique identifier |

**Response:** `200 OK` — `NotificationDto`

**Error:** `404 Not Found` — if the notification does not exist for the given tenant.

---

### GET `/v1/notifications/`

List notifications for the current tenant.

**Headers:** `X-Tenant-Id` required.

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `limit` | `integer` | No | `50` | Maximum number of items to return |
| `offset` | `integer` | No | `0` | Number of items to skip |

**Response:** `200 OK` — `NotificationDto[]`

---

### NotificationResultDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Notification unique identifier |
| `status` | `string` | No | Delivery status (e.g., `sent`, `blocked`) |
| `providerUsed` | `string` | Yes | Provider that handled delivery |
| `platformFallbackUsed` | `boolean` | No | Whether platform fallback provider was used |
| `blockedByPolicy` | `boolean` | No | Whether notification was blocked by policy |
| `blockedReasonCode` | `string` | Yes | Reason code if blocked |
| `overrideUsed` | `boolean` | No | Whether a suppression override was applied |
| `failureCategory` | `string` | Yes | Category of failure if delivery failed |
| `lastErrorMessage` | `string` | Yes | Last error message if delivery failed |

---

### NotificationDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `tenantId` | `guid` | Yes | Tenant identifier |
| `channel` | `string` | No | Delivery channel |
| `status` | `string` | No | Current status |
| `recipientJson` | `string` | No | Serialized recipient details |
| `messageJson` | `string` | No | Serialized message content |
| `metadataJson` | `string` | Yes | Serialized metadata |
| `idempotencyKey` | `string` | Yes | Idempotency key |
| `providerUsed` | `string` | Yes | Provider that handled delivery |
| `failureCategory` | `string` | Yes | Category of failure |
| `lastErrorMessage` | `string` | Yes | Last error message |
| `templateId` | `guid` | Yes | Template used for rendering |
| `templateVersionId` | `guid` | Yes | Template version used |
| `templateKey` | `string` | Yes | Template key |
| `renderedSubject` | `string` | Yes | Rendered subject line |
| `renderedBody` | `string` | Yes | Rendered HTML body |
| `renderedText` | `string` | Yes | Rendered plain-text body |
| `providerOwnershipMode` | `string` | Yes | Provider ownership mode used |
| `providerConfigId` | `guid` | Yes | Provider config used |
| `platformFallbackUsed` | `boolean` | No | Whether platform fallback was used |
| `blockedByPolicy` | `boolean` | No | Whether blocked by policy |
| `blockedReasonCode` | `string` | Yes | Reason code if blocked |
| `overrideUsed` | `boolean` | No | Whether suppression override was applied |
| `createdAt` | `datetime` | No | Record creation timestamp |
| `updatedAt` | `datetime` | No | Record last-updated timestamp |

---

## Templates Endpoints

Base path: `/v1/templates`

### GET `/v1/templates/`

List templates for the current tenant.

**Headers:** `X-Tenant-Id` required.

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `limit` | `integer` | No | `50` | Maximum number of items to return |
| `offset` | `integer` | No | `0` | Number of items to skip |

**Response:** `200 OK` — `TemplateDto[]`

---

### GET `/v1/templates/{id}`

Get a template by its unique identifier. Returns `404` if the template does not exist or belongs to a different tenant.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Template unique identifier |

**Response:** `200 OK` — `TemplateDto`

**Error:** `404 Not Found` — if the template does not exist or does not belong to the tenant.

---

### POST `/v1/templates/`

Create a new template.

**Headers:** `X-Tenant-Id` required.

**Request Body: `CreateTemplateDto`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `templateKey` | `string` | Yes | No | Unique template key identifier |
| `channel` | `string` | Yes | No | Channel this template is for (e.g., `email`, `sms`) |
| `name` | `string` | Yes | No | Human-readable template name |
| `description` | `string` | No | Yes | Template description |
| `scope` | `string` | No | Yes | Template scope |
| `productType` | `string` | No | Yes | Product type association |

**Response:** `201 Created` — `TemplateDto`

Returns the created template with a `Location` header pointing to `/v1/templates/{id}`.

---

### PUT `/v1/templates/{id}`

Update an existing template. Returns `404` if the template does not exist or belongs to a different tenant.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Template unique identifier |

**Request Body: `UpdateTemplateDto`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `name` | `string` | No | Yes | Updated template name |
| `description` | `string` | No | Yes | Updated description |
| `status` | `string` | No | Yes | Updated status |

**Response:** `200 OK` — `TemplateDto`

**Error:** `404 Not Found` — if the template does not exist or does not belong to the tenant.

---

### DELETE `/v1/templates/{id}`

Delete a template. Returns `404` if the template does not exist or belongs to a different tenant.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Template unique identifier |

**Response:** `204 No Content`

**Error:** `404 Not Found` — if the template does not exist or does not belong to the tenant.

---

### POST `/v1/templates/{templateId}/versions`

Create a new version for a template.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `templateId` | `guid` | Parent template unique identifier |

**Request Body: `CreateTemplateVersionDto`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `subjectTemplate` | `string` | No | Yes | Subject line template (supports variable substitution) |
| `bodyTemplate` | `string` | Yes | No | Body content template |
| `textTemplate` | `string` | No | Yes | Plain-text body template |
| `editorType` | `string` | No | Yes | Editor type used to create the template (e.g., `html`, `drag-drop`) |

**Response:** `201 Created` — `TemplateVersionDto`

Returns the created version with a `Location` header pointing to `/v1/templates/{templateId}/versions/{id}`.

**Error:** `404 Not Found` — if the parent template does not exist or does not belong to the tenant.

---

### GET `/v1/templates/{templateId}/versions`

List all versions of a template.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `templateId` | `guid` | Parent template unique identifier |

**Response:** `200 OK` — `TemplateVersionDto[]`

**Error:** `404 Not Found` — if the parent template does not exist or does not belong to the tenant.

---

### POST `/v1/templates/{templateId}/versions/{versionId}/publish`

Publish a specific template version, making it the active version used for rendering.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `templateId` | `guid` | Parent template unique identifier |
| `versionId` | `guid` | Version unique identifier |

**Response:** `200 OK` — `TemplateVersionDto`

**Error:** `404 Not Found` — if the parent template does not exist or does not belong to the tenant.

---

### TemplateDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `tenantId` | `guid` | Yes | Owning tenant ID (`null` for global templates) |
| `templateKey` | `string` | No | Template key identifier |
| `channel` | `string` | No | Channel (e.g., `email`, `sms`) |
| `name` | `string` | No | Human-readable name |
| `description` | `string` | Yes | Description |
| `status` | `string` | No | Current status |
| `scope` | `string` | No | Template scope |
| `productType` | `string` | Yes | Associated product type |
| `createdAt` | `datetime` | No | Record creation timestamp |
| `updatedAt` | `datetime` | No | Record last-updated timestamp |

---

### TemplateVersionDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `templateId` | `guid` | No | Parent template ID |
| `versionNumber` | `integer` | No | Sequential version number |
| `subjectTemplate` | `string` | Yes | Subject line template |
| `bodyTemplate` | `string` | No | Body content template |
| `textTemplate` | `string` | Yes | Plain-text body template |
| `editorType` | `string` | Yes | Editor type used |
| `isPublished` | `boolean` | No | Whether this version is currently published |
| `publishedBy` | `string` | Yes | Who published this version |
| `publishedAt` | `datetime` | Yes | When this version was published |
| `createdAt` | `datetime` | No | Record creation timestamp |
| `updatedAt` | `datetime` | No | Record last-updated timestamp |

---

## Global Templates Endpoints

Base path: `/v1/templates/global`

Global templates are not associated with any tenant and are available to all tenants. The created templates have a `null` `tenantId`.

> **Note:** The `X-Tenant-Id` header is still required by the tenant middleware for these endpoints (the path is not exempted), even though the tenant ID is not used by the endpoint logic itself.

### GET `/v1/templates/global/`

List global templates.

**Headers:** `X-Tenant-Id` required (by middleware).

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `limit` | `integer` | No | `50` | Maximum number of items to return |
| `offset` | `integer` | No | `0` | Number of items to skip |

**Response:** `200 OK` — `TemplateDto[]`

---

### POST `/v1/templates/global/`

Create a new global template.

**Headers:** `X-Tenant-Id` required (by middleware).

**Request Body: `CreateTemplateDto`**

See [POST `/v1/templates/`](#post-v1templates-1) for field details. The request body uses the same `CreateTemplateDto` fields: `templateKey`, `channel`, `name`, `description`, `scope`, `productType`.

**Response:** `201 Created` — `TemplateDto`

Returns the created template with a `Location` header pointing to `/v1/templates/{id}`.

---

## Providers Endpoints

Base path: `/v1/providers`

### GET `/v1/providers/configs`

List provider configurations for the current tenant.

**Headers:** `X-Tenant-Id` required.

**Query Parameters:**

| Parameter | Type | Required | Description |
|---|---|---|---|
| `channel` | `string` | No | Filter by channel (e.g., `email`, `sms`) |

**Response:** `200 OK` — `TenantProviderConfigDto[]`

---

### GET `/v1/providers/configs/{id}`

Get a provider configuration by its unique identifier.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Provider config unique identifier |

**Response:** `200 OK` — `TenantProviderConfigDto`

**Error:** `404 Not Found` — if the config does not exist for the given tenant.

---

### POST `/v1/providers/configs`

Create a new provider configuration.

**Headers:** `X-Tenant-Id` required.

**Request Body: `CreateTenantProviderConfigDto`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `channel` | `string` | Yes | No | Channel (e.g., `email`, `sms`) |
| `providerType` | `string` | Yes | No | Provider type (e.g., `sendgrid`, `twilio`) |
| `displayName` | `string` | Yes | No | Human-readable display name |
| `credentialsJson` | `string` | Yes | No | Serialized provider credentials (JSON) |
| `settingsJson` | `string` | No | Yes | Serialized provider settings (JSON) |
| `priority` | `integer` | No | Yes | Priority order for provider selection |

**Response:** `201 Created` — `TenantProviderConfigDto`

Returns the created config with a `Location` header pointing to `/v1/providers/configs/{id}`.

---

### PUT `/v1/providers/configs/{id}`

Update an existing provider configuration.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Provider config unique identifier |

**Request Body: `UpdateTenantProviderConfigDto`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `displayName` | `string` | No | Yes | Updated display name |
| `credentialsJson` | `string` | No | Yes | Updated serialized credentials (JSON) |
| `settingsJson` | `string` | No | Yes | Updated serialized settings (JSON) |
| `status` | `string` | No | Yes | Updated status |
| `priority` | `integer` | No | Yes | Updated priority |

**Response:** `200 OK` — `TenantProviderConfigDto`

---

### DELETE `/v1/providers/configs/{id}`

Delete a provider configuration.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Provider config unique identifier |

**Response:** `204 No Content`

---

### POST `/v1/providers/configs/{id}/validate`

Validate a provider configuration's credentials and settings.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Provider config unique identifier |

**Response:** `200 OK` — Validation result object.

---

### POST `/v1/providers/configs/{id}/health-check`

Run a health check against a provider configuration.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Provider config unique identifier |

**Response:** `200 OK` — Health check result object.

---

### GET `/v1/providers/channel-settings`

List channel provider settings for the current tenant.

**Headers:** `X-Tenant-Id` required.

**Response:** `200 OK` — `TenantChannelSettingDto[]`

---

### PUT `/v1/providers/channel-settings/{channel}`

Create or update channel provider settings for a specific channel.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `channel` | `string` | Channel name (e.g., `email`, `sms`) |

**Request Body: `UpdateChannelSettingDto`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `providerMode` | `string` | No | Yes | Provider mode (defaults to `platform_managed`) |
| `primaryTenantProviderConfigId` | `guid` | No | Yes | Primary provider config ID |
| `fallbackTenantProviderConfigId` | `guid` | No | Yes | Fallback provider config ID |
| `allowPlatformFallback` | `boolean` | No | Yes | Allow platform fallback (defaults to `true`) |
| `allowAutomaticFailover` | `boolean` | No | Yes | Allow automatic failover (defaults to `true`) |

**Response:** `200 OK` — Upserted channel setting entity.

---

### TenantProviderConfigDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `tenantId` | `guid` | No | Owning tenant ID |
| `channel` | `string` | No | Channel (e.g., `email`, `sms`) |
| `providerType` | `string` | No | Provider type (e.g., `sendgrid`, `twilio`) |
| `displayName` | `string` | No | Human-readable display name |
| `settingsJson` | `string` | No | Serialized settings (JSON) |
| `status` | `string` | No | Current status |
| `validationStatus` | `string` | No | Credential validation status |
| `validationMessage` | `string` | Yes | Validation failure message |
| `lastValidatedAt` | `datetime` | Yes | Last validation timestamp |
| `healthStatus` | `string` | No | Health check status |
| `lastHealthCheckAt` | `datetime` | Yes | Last health check timestamp |
| `healthCheckLatencyMs` | `integer` | Yes | Last health check latency in milliseconds |
| `priority` | `integer` | No | Priority order |
| `createdAt` | `datetime` | No | Record creation timestamp |
| `updatedAt` | `datetime` | No | Record last-updated timestamp |

---

### TenantChannelSettingDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `tenantId` | `guid` | No | Owning tenant ID |
| `channel` | `string` | No | Channel name |
| `providerMode` | `string` | No | Provider mode (e.g., `platform_managed`, `tenant_managed`) |
| `primaryTenantProviderConfigId` | `guid` | Yes | Primary provider config ID |
| `fallbackTenantProviderConfigId` | `guid` | Yes | Fallback provider config ID |
| `allowPlatformFallback` | `boolean` | No | Whether platform fallback is allowed |
| `allowAutomaticFailover` | `boolean` | No | Whether automatic failover is allowed |
| `createdAt` | `datetime` | No | Record creation timestamp |
| `updatedAt` | `datetime` | No | Record last-updated timestamp |

---

## Webhooks Endpoints

Base path: `/v1/webhooks`

Webhook endpoints receive delivery status callbacks from external providers. They are exempt from tenant context resolution (`X-Tenant-Id` not required). Authentication is handled via provider-specific signature verification.

### POST `/v1/webhooks/sendgrid`

Receive SendGrid event webhooks (delivery, bounce, open, click, etc.).

**Headers:** No `X-Tenant-Id` required. SendGrid signature headers are used for authentication.

**Request Body:** Raw SendGrid event payload (JSON array of event objects).

**Response:**

| Status | Body | Condition |
|---|---|---|
| `200 OK` | `{ "status": "accepted" }` | Webhook accepted and processed |
| `401 Unauthorized` | `{ "error": "<reason>" }` | Signature verification failed |

---

### POST `/v1/webhooks/twilio`

Receive Twilio status callback webhooks.

**Headers:** No `X-Tenant-Id` required. Twilio signature headers are used for authentication.

**Request Body:** Form-encoded Twilio callback parameters.

**Response:**

| Status | Body | Condition |
|---|---|---|
| `200 OK` | `{ "status": "accepted" }` | Webhook accepted and processed |
| `401 Unauthorized` | `{ "error": "<reason>" }` | Signature verification failed |

---

## Contacts Endpoints

Base path: `/v1/contacts`

### GET `/v1/contacts/suppressions`

List contact suppressions for the current tenant.

**Headers:** `X-Tenant-Id` required.

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `limit` | `integer` | No | `50` | Maximum number of items to return |
| `offset` | `integer` | No | `0` | Number of items to skip |

**Response:** `200 OK` — `ContactSuppressionDto[]`

---

### POST `/v1/contacts/suppressions`

Create a new contact suppression.

**Headers:** `X-Tenant-Id` required.

**Request Body: `CreateContactSuppressionDto`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `channel` | `string` | Yes | No | Channel to suppress (e.g., `email`, `sms`) |
| `contactValue` | `string` | Yes | No | Contact value to suppress (e.g., email address, phone number) |
| `suppressionType` | `string` | Yes | No | Type of suppression (e.g., `bounce`, `complaint`, `manual`) |
| `reason` | `string` | No | Yes | Reason for suppression |
| `expiresAt` | `datetime` | No | Yes | Expiration timestamp (suppression is permanent if not set) |
| `notes` | `string` | No | Yes | Additional notes |

**Response:** `201 Created` — `ContactSuppressionDto`

Returns the created suppression with a `Location` header pointing to `/v1/contacts/suppressions/{id}`.

---

### DELETE `/v1/contacts/suppressions/{id}`

Delete a contact suppression. Returns `404` if the suppression does not exist or belongs to a different tenant.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Suppression unique identifier |

**Response:** `204 No Content`

**Error:** `404 Not Found` — if the suppression does not exist or does not belong to the tenant.

---

### GET `/v1/contacts/health`

Look up the delivery health of a specific contact.

**Headers:** `X-Tenant-Id` required.

**Query Parameters:**

| Parameter | Type | Required | Description |
|---|---|---|---|
| `channel` | `string` | Yes | Channel (e.g., `email`, `sms`) |
| `contactValue` | `string` | Yes | Contact value to look up (e.g., email address, phone number) |

**Response:** `200 OK` — Contact health record.

**Error:** `404 Not Found` — if no health record exists for the specified contact.

---

### ContactSuppressionDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `tenantId` | `guid` | No | Owning tenant ID |
| `channel` | `string` | No | Suppressed channel |
| `contactValue` | `string` | No | Suppressed contact value |
| `suppressionType` | `string` | No | Type of suppression |
| `status` | `string` | No | Current status |
| `reason` | `string` | Yes | Reason for suppression |
| `source` | `string` | Yes | Source of suppression (e.g., `api`, `webhook`) |
| `expiresAt` | `datetime` | Yes | Expiration timestamp |
| `createdBy` | `string` | Yes | Who created the suppression |
| `notes` | `string` | Yes | Additional notes |
| `createdAt` | `datetime` | No | Record creation timestamp |
| `updatedAt` | `datetime` | No | Record last-updated timestamp |

---

## Branding Endpoints

Base path: `/v1/branding`

### GET `/v1/branding/`

List all branding configurations for the current tenant.

**Headers:** `X-Tenant-Id` required.

**Response:** `200 OK` — `BrandingDto[]`

---

### GET `/v1/branding/{productType}`

Get branding configuration for a specific product type.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `productType` | `string` | Product type identifier |

**Response:** `200 OK` — `BrandingDto`

**Error:** `404 Not Found` — if no branding exists for the specified product type.

---

### PUT `/v1/branding/`

Create or update branding configuration. Upserts based on the tenant and product type combination.

**Headers:** `X-Tenant-Id` required.

**Request Body: `UpsertBrandingDto`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `productType` | `string` | Yes | No | Product type identifier |
| `brandName` | `string` | Yes | No | Brand display name |
| `logoUrl` | `string` | No | Yes | URL to brand logo |
| `primaryColor` | `string` | No | Yes | Primary brand color (hex) |
| `secondaryColor` | `string` | No | Yes | Secondary brand color (hex) |
| `accentColor` | `string` | No | Yes | Accent color (hex) |
| `textColor` | `string` | No | Yes | Text color (hex) |
| `backgroundColor` | `string` | No | Yes | Background color (hex) |
| `buttonRadius` | `string` | No | Yes | Button border radius (CSS value) |
| `fontFamily` | `string` | No | Yes | Font family (CSS value) |
| `supportEmail` | `string` | No | Yes | Support email address |
| `supportPhone` | `string` | No | Yes | Support phone number |
| `websiteUrl` | `string` | No | Yes | Brand website URL |
| `emailHeaderHtml` | `string` | No | Yes | Custom HTML for email header |
| `emailFooterHtml` | `string` | No | Yes | Custom HTML for email footer |

**Response:** `200 OK` — `BrandingDto`

---

### GET `/v1/branding/resolved/{productType}`

Get the fully resolved branding for a product type. Resolution may merge tenant-specific branding with platform defaults.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `productType` | `string` | Product type identifier |

**Response:** `200 OK` — Resolved branding object.

---

### BrandingDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `tenantId` | `guid` | No | Owning tenant ID |
| `productType` | `string` | No | Product type identifier |
| `brandName` | `string` | No | Brand display name |
| `logoUrl` | `string` | Yes | URL to brand logo |
| `primaryColor` | `string` | Yes | Primary brand color |
| `secondaryColor` | `string` | Yes | Secondary brand color |
| `accentColor` | `string` | Yes | Accent color |
| `textColor` | `string` | Yes | Text color |
| `backgroundColor` | `string` | Yes | Background color |
| `buttonRadius` | `string` | Yes | Button border radius |
| `fontFamily` | `string` | Yes | Font family |
| `supportEmail` | `string` | Yes | Support email address |
| `supportPhone` | `string` | Yes | Support phone number |
| `websiteUrl` | `string` | Yes | Brand website URL |
| `emailHeaderHtml` | `string` | Yes | Custom email header HTML |
| `emailFooterHtml` | `string` | Yes | Custom email footer HTML |
| `createdAt` | `datetime` | No | Record creation timestamp |
| `updatedAt` | `datetime` | No | Record last-updated timestamp |

---

## Billing Endpoints

Base path: `/v1/billing`

### GET `/v1/billing/plan`

Get the active billing plan for the current tenant.

**Headers:** `X-Tenant-Id` required.

**Response:** `200 OK` — `BillingPlanDto`

**Error:** `404 Not Found` — if no active plan exists for the tenant.

---

### GET `/v1/billing/plans`

List all billing plans for the current tenant.

**Headers:** `X-Tenant-Id` required.

**Response:** `200 OK` — `BillingPlanDto[]`

---

### GET `/v1/billing/rates/{planId}`

Get billing rates for a specific plan. Returns `404` if the plan does not exist or belongs to a different tenant.

**Headers:** `X-Tenant-Id` required.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `planId` | `guid` | Billing plan unique identifier |

**Response:** `200 OK` — `BillingRateDto[]`

**Error:** `404 Not Found` — if the plan does not exist or does not belong to the tenant.

---

### GET `/v1/billing/rate-limits`

Get rate limit policies for the current tenant.

**Headers:** `X-Tenant-Id` required.

**Response:** `200 OK` — Array of rate limit policy objects.

---

### BillingPlanDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `tenantId` | `guid` | No | Owning tenant ID |
| `planName` | `string` | No | Plan name |
| `billingMode` | `string` | No | Billing mode (e.g., `flat`, `usage_based`) |
| `status` | `string` | No | Plan status |
| `monthlyFlatRate` | `decimal` | Yes | Monthly flat rate (if applicable) |
| `currency` | `string` | No | Currency code (e.g., `USD`) |
| `effectiveFrom` | `datetime` | Yes | Plan effective start date |
| `effectiveTo` | `datetime` | Yes | Plan effective end date |
| `createdAt` | `datetime` | No | Record creation timestamp |
| `updatedAt` | `datetime` | No | Record last-updated timestamp |

---

### BillingRateDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `billingPlanId` | `guid` | No | Parent billing plan ID |
| `usageUnit` | `string` | No | Usage unit being billed |
| `channel` | `string` | Yes | Channel this rate applies to |
| `providerOwnershipMode` | `string` | Yes | Provider ownership mode |
| `unitPrice` | `decimal` | No | Price per unit |
| `currency` | `string` | No | Currency code |
| `isBillable` | `boolean` | No | Whether this usage is billable |
| `createdAt` | `datetime` | No | Record creation timestamp |
| `updatedAt` | `datetime` | No | Record last-updated timestamp |

---

## Internal Endpoints

Base path: `/internal`

Internal endpoints are for service-to-service communication. They are exempt from tenant context resolution (`X-Tenant-Id` not required). They require the `X-Internal-Service-Token` header for authentication (see [Authentication](#authentication)).

### POST `/internal/send-email`

Send an email directly via the platform email provider, bypassing tenant routing and notification tracking.

**Headers:** `X-Internal-Service-Token` required. No `X-Tenant-Id` required.

**Request Body: `InternalSendEmailDto`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `to` | `string` | Yes | No | Recipient email address |
| `from` | `string` | No | Yes | Sender email address (uses platform default if not specified) |
| `subject` | `string` | Yes | No | Email subject line |
| `body` | `string` | Yes | No | Plain-text email body |
| `html` | `string` | No | Yes | HTML email body |
| `replyTo` | `string` | No | Yes | Reply-to email address |

**Response:**

| Status | Body | Condition |
|---|---|---|
| `200 OK` | `InternalSendEmailResultDto` | Email sent successfully |
| `502 Bad Gateway` | `InternalSendEmailResultDto` | Email delivery failed |

### InternalSendEmailResultDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `success` | `boolean` | No | Whether the email was sent successfully |
| `error` | `string` | Yes | Error message if sending failed |
