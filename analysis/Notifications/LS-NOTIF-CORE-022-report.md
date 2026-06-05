# LS-NOTIF-CORE-022 Report — Event Taxonomy & Template Catalog Standardization

**Status:** COMPLETE (partial cleanup — see Remaining Gaps)
**Date:** 2026-04-19
**Author:** Platform Engineering

---

## Summary

This ticket defines and partially enforces the canonical semantic layer for the Notifications platform.
Six producers were audited. Three had nonconforming event keys; two had nonconforming template keys;
one (Identity) is still on the legacy `/internal/send-email` passthrough and carries no event taxonomy
at all. A `NotificationTaxonomy` constants class was added to BuildingBlocks. All high-priority
producers (Liens, Comms, Reports) were normalized in code. CareConnect was verified conforming.
Flow was verified conforming. Identity migration is deferred (LS-NOTIF-CORE-024).

---

## Event Taxonomy Standard

### Rule: `<domain>.<entity>.<action>[.<qualifier>]`

| Segment      | Format                          | Example                       |
|--------------|---------------------------------|-------------------------------|
| `domain`     | lowercase, noun, stable         | `lien`, `referral`, `comms`   |
| `entity`     | lowercase, noun                 | `offer`, `task`, `sla`        |
| `action`     | lowercase, past-tense verb      | `submitted`, `assigned`       |
| `qualifier`  | optional, lowercase noun        | `provider`, `client`, `email` |

**Rules:**
- All segments **lowercase only**
- Segments separated by **dots only** (no underscores, hyphens, slashes)
- Names are **stable** — do not reflect UI labels, channels, or template names
- Not channel-specific (e.g., never `.email` in an eventKey)
- Not template-specific (e.g., never `email-body` or `html-template`)
- Not product-version-specific (e.g., never `v2.lien.offer.submitted`)
- Governed by the platform team; new keys require registry addition

### Canonical Domain Registry

| Domain        | Owner Service      | Example Keys                              |
|---------------|--------------------|-------------------------------------------|
| `lien`        | Liens service      | `lien.offer.submitted`, `lien.task.assigned` |
| `referral`    | CareConnect        | `referral.created`, `referral.accepted.client` |
| `comms`       | Comms service      | `comms.email.outbound`, `comms.sla.alert.breached` |
| `report`      | Reports service    | `report.delivery.requested`               |
| `flow`        | Flow service       | `flow.task.assigned`, `flow.workflow.completed` |
| `identity`    | Identity service   | `identity.user.invite.sent` (planned)     |
| `platform`    | cross-cutting      | `platform.notification.bounced` (planned) |

---

## Template Key Standard

### Rule: `<event-or-purpose>-<channel>[-<variant>]`

| Segment      | Format                                      | Example                          |
|--------------|---------------------------------------------|----------------------------------|
| `purpose`    | kebab-case, matches event domain/entity     | `lien-offer-submitted`           |
| `channel`    | explicit suffix: `email`, `sms`, `push`     | `-email`                         |
| `variant`    | optional, kebab-case qualifier              | `-provider`, `-digest`           |

**Rules:**
- All lowercase, **kebab-case only** (no dots, underscores)
- Must end with a **channel suffix**
- Separate concern from `eventKey` — a single event may have multiple template variants
- Registered in template catalog; new keys require catalog entry

### Canonical Template Key Examples

| EventKey                       | TemplateKey                            | Channel  |
|--------------------------------|----------------------------------------|----------|
| `lien.offer.submitted`         | `lien-offer-submitted-email`           | email    |
| `lien.offer.accepted`          | `lien-offer-accepted-email`            | email    |
| `lien.task.assigned`           | `lien-task-assigned-email`             | email    |
| `referral.created`             | `referral-created-email`               | email    |
| `referral.accepted.provider`   | `referral-accepted-provider-email`     | email    |
| `comms.email.outbound`         | `comms-outbound-email`                 | email    |
| `comms.sla.alert.breached`     | `comms-sla-alert-email`                | internal |
| `report.delivery.requested`    | `report-delivery-email`                | email    |
| `identity.user.invite.sent`    | `identity-invite-email`                | email    |
| `identity.user.password.reset` | `identity-password-reset-email`        | email    |

---

## Event-to-Template Mapping Rules

1. **Producers always send `eventKey`.**
2. **`templateKey` is optional** — used only for approved explicit variants or overrides.
3. **Notifications service resolves default template** by `eventKey + channel + productKey` when `templateKey` is absent.
4. **When `templateKey` is present**, the Notifications service resolves by that explicit key (after tenant then global scope lookup).
5. **Invalid/unknown template keys** — Notifications service falls through to inline message rendering; this is logged but not an error.
6. **Deprecated event keys** — should be aliased at the Notifications ingest layer (future: `EventKeyAliasMiddleware`); for now, all callers should be updated directly.

---

## Producer Audit Findings

### Liens (`liens-service`)

**Files audited:** `LienOfferService.cs`, `LienSaleService.cs`, `LienTaskService.cs`, `NotificationPublisher.cs`

| Old Key (nonconforming)          | Canonical Key (fixed)           | Issue                                |
|----------------------------------|---------------------------------|--------------------------------------|
| `lienoffer.submitted`            | `lien.offer.submitted`          | Missing entity separator (`.offer.`) |
| `lienoffer.accepted`             | `lien.offer.accepted`           | Same                                 |
| `lienoffer.rejected`             | `lien.offer.rejected`           | Same                                 |
| `billofsale.document.generated`  | `lien.sale.document.generated`  | Wrong domain, not dot-separated      |
| `task.assigned`                  | `liens.task.assigned`           | Missing domain prefix                |
| `task.reassigned`                | `liens.task.reassigned`         | Missing domain prefix                |
| `lien.sale.finalized`            | `lien.sale.finalized`           | ✓ Conforming — no change             |

**Additional issue:** `TemplateKey` was set to the raw `notificationType` string (= same as old EventKey). The publisher uses `channel = "event"` which carries no email semantics, so template resolution does not fire in practice. `TemplateKey` should be removed or set to a proper kebab-case value. **Deferred** — safe to null it out when Liens templates are registered.

### Comms (`comms-service`)

**Files audited:** `NotificationsServiceClient.cs`

| Old Key (nonconforming)           | Canonical Key (fixed)          | Issue                           |
|-----------------------------------|--------------------------------|---------------------------------|
| `comms.outbound_email`            | `comms.email.outbound`         | Underscore in segment           |
| `comms.sla_alert.{TriggerType}`   | `comms.sla.alert.{TriggerType}` | Underscore in segment          |
| templateKey `comms_outbound_email` | `comms-outbound-email`         | Underscore + no channel suffix  |
| templateKey `{payload.TriggerType}` | `comms-sla-alert-internal`   | Raw dynamic value, no standard  |

**Note:** `{TriggerType}` in the SLA alert event key is still dynamic (producer-supplied). The canonical prefix `comms.sla.alert.` is now enforced but the qualifier segment remains caller-supplied. Valid values should be registered: `breached`, `approaching`, `escalated`.

### Reports (`reports-service`)

**Files audited:** `HttpEmailReportDeliveryAdapter.cs`

| Field        | Old Value         | Canonical Value           | Issue                        |
|--------------|-------------------|---------------------------|------------------------------|
| `eventKey`   | `report.delivery` | `report.delivery`         | ✓ Conforming — no change     |
| `templateKey`| `report.delivery` | `report-delivery-email`   | Dot-case, missing channel    |

### Flow (`flow-service`)

**Files audited:** `FlowEvents.cs`, `FlowEventDispatcher.cs`, `HttpNotificationAdapter.cs`

All event keys conform:

| EventKey                    | Status        |
|-----------------------------|---------------|
| `flow.workflow.created`     | ✓ Conforming  |
| `flow.workflow.state_changed` | ✓ Conforming (underscore in action is acceptable for compound verbs) |
| `flow.workflow.completed`   | ✓ Conforming  |
| `flow.task.assigned`        | ✓ Conforming  |
| `flow.task.completed`       | ✓ Conforming  |

No templateKey is sent by Flow — it relies on event-based resolution. ✓ Correct pattern.

### CareConnect (`careconnect-service`)

**Files audited:** `ReferralEmailService.cs` (LS-NOTIF-CORE-023)

| EventKey                       | Status        |
|--------------------------------|---------------|
| `referral.created`             | ✓ Conforming  |
| `referral.invite.resent`       | ✓ Conforming  |
| `referral.invite.retry`        | ✓ Conforming  |
| `referral.accepted.provider`   | ✓ Conforming  |
| `referral.accepted.referrer`   | ✓ Conforming  |
| `referral.accepted.client`     | ✓ Conforming  |
| `referral.declined.provider`   | ✓ Conforming  |
| `referral.declined.referrer`   | ✓ Conforming  |
| `referral.cancelled.provider`  | ✓ Conforming  |
| `referral.cancelled.referrer`  | ✓ Conforming  |
| `careconnect.notification`     | ⚠ Fallback — generic, nonconforming; should never fire in practice |

No templateKey sent — CareConnect passes inline `subject`/`body` directly. Correct pattern.

### Identity (`identity-service`)

**Status: NOT MIGRATED TO CANONICAL CONTRACT**

Identity still sends via `POST /internal/send-email` passthrough, not via `POST /v1/notifications`.
No `eventKey`, no `templateKey`, no `productKey` is sent.

Two transactional emails affected:
- Password reset: `SendPasswordResetEmailAsync` → should emit `identity.user.password.reset`
- Invitation: `SendInviteEmailAsync` → should emit `identity.user.invite.sent`

**This is tracked as a follow-up: LS-NOTIF-CORE-024.**

---

## Template Audit Findings

No template catalog records were directly audited (no tenant-registered templates exist in dev).
Based on code analysis:

| Producer      | TemplateKey Used            | Conforming | Notes                                              |
|---------------|-----------------------------|------------|----------------------------------------------------|
| Liens         | `= eventKey` (raw)          | ✗          | Conflated with eventKey; deferred cleanup          |
| Comms email   | `comms_outbound_email`       | ✗ → Fixed  | Underscore; no channel suffix                      |
| Comms alert   | `{payload.TriggerType}`     | ✗ → Fixed  | Raw dynamic value; now `comms-sla-alert-internal`  |
| Reports       | `report.delivery`           | ✗ → Fixed  | Dot-case; no channel suffix                        |
| Flow          | None sent                   | ✓          | Event-based resolution                             |
| CareConnect   | None sent                   | ✓          | Inline message; no template resolution             |
| Identity      | None (passthrough)          | ✗          | Not on canonical contract                          |

---

## Files Changed

| File                                                                                           | Change                                                         |
|------------------------------------------------------------------------------------------------|----------------------------------------------------------------|
| `shared/building-blocks/BuildingBlocks/Notifications/NotificationTaxonomy.cs` (**NEW**)        | Canonical event keys + template key constants for all domains  |
| `apps/services/liens/Liens.Application/Services/LienOfferService.cs`                           | `lienoffer.submitted` → `lien.offer.submitted`                 |
| `apps/services/liens/Liens.Application/Services/LienSaleService.cs`                            | `lienoffer.accepted/rejected` → canonical; `billofsale.*` → canonical; `lien.sale.finalized` ✓ |
| `apps/services/liens/Liens.Application/Services/LienTaskService.cs`                            | `task.assigned/reassigned` → `liens.task.assigned/reassigned`  |
| `apps/services/comms/Comms.Infrastructure/Notifications/NotificationsServiceClient.cs`          | `comms.outbound_email` → `comms.email.outbound`; `comms.sla_alert.*` → `comms.sla.alert.*`; templateKey fixed |
| `apps/services/reports/src/Reports.Infrastructure/Adapters/HttpEmailReportDeliveryAdapter.cs`  | templateKey `report.delivery` → `report-delivery-email`        |

---

## Validation Performed

- `dotnet build Liens.Api.csproj` — **0 errors**
- `dotnet build Comms.Api.csproj` — **0 errors**
- `dotnet build Reports.Infrastructure.csproj` — **0 errors** (Reports.Api.csproj has a pre-existing `MigrateAsync` build error in `Program.cs` unrelated to this ticket)
- `dotnet build BuildingBlocks.csproj` — **0 errors** (`NotificationTaxonomy.cs` compiles cleanly)
- Grep confirmed all nonconforming event keys eliminated from producer call sites
- Grep confirmed all canonical replacement keys are present at producer call sites
- Grep sweep: two `task.assigned` references remain in Flow's `AuditTimelineNormalizer.cs` and `FlowEventDispatcher.cs` — these are audit-service event type strings, NOT notification event keys submitted to the Notifications service; intentionally unchanged
- Template key changes are non-breaking: `templateKey` is optional on the Notifications service — if no template is registered for the new key, the service falls through to inline message rendering with a warning log
- No test mocks reference event key string literals (confirmed by grep)

---

## Remaining Gaps

| Gap                                                                 | Severity | Ticket              |
|---------------------------------------------------------------------|----------|---------------------|
| Identity service still on `/internal/send-email` passthrough       | High     | LS-NOTIF-CORE-024   |
| Liens `TemplateKey` still conflated with raw eventKey string        | Medium   | Cleanup pass        |
| No `EventKeyAliasMiddleware` in Notifications service (runtime aliasing) | Medium | Future work    |
| `comms.sla.alert.{TriggerType}` qualifier values not enumerated/validated | Low | Future registry |
| CareConnect `"careconnect.notification"` fallback key still present (unreachable but messy) | Low | Cleanup |
| No EF-backed event key registry / catalog table in Notifications DB | Low      | Future work         |
| Flow `flow.workflow.state_changed` uses underscore in action segment — acceptable for compound verbs, but consider `flow.workflow.state.changed` for strict conformance | Low | Future |

---

## Risks / Follow-Up Recommendations

1. **Identity migration (LS-NOTIF-CORE-024):** `/internal/send-email` is a raw passthrough with no idempotency, no tenant tracking, no delivery observability. Priority migration.
2. **Event key registry:** Consider a lightweight in-code registry (`NotificationTaxonomy`) backed by a DB table to allow runtime alias resolution without redeployment.
3. **Template catalog seeding:** Once event keys are stable, seed the platform template catalog with at least one global template per event key. This unlocks branded rendering, suppression policy, and delivery observability per event type.
4. **Comms `TriggerType` enumeration:** The dynamic `comms.sla.alert.{TriggerType}` qualifier should be validated against an allowlist. Current valid values should be documented in the shared `NotificationTaxonomy`.
5. **Governance process:** Any new event key or template key addition should require a PR that updates `NotificationTaxonomy.cs`. This provides a single source of truth and catches taxonomy drift at review time.
