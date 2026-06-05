# BLK-CC-02 Commit Summary

## Commit ID

`ebd1cdf1a9bddd70fd3b252410edb46f83fae59c`

## Commit Message

> Add ability to resume provider onboarding after partial failures
>
> Introduces a resumable onboarding flow for providers, saving pending tenant
> information after successful tenant provisioning and before identity assignment.
> This allows retries to bypass tenant re-provisioning if identity assignment fails,
> preventing permanent onboarding blocks. The API response now includes an `IsResumed`
> flag to indicate when a prior partial attempt was completed.

## Author / Date

- **Author:** Agent
- **Date:** 2026-04-23 19:23:49 UTC

## Stats

| Metric | Value |
|--------|-------|
| Files changed | 11 |
| Insertions | 992 |
| Deletions | 70 |
| Net lines | +922 |

---

## Files Changed

| File | Change |
|------|--------|
| `analysis/BLK-CC-02-report.md` | New — block report (239 lines) |
| `analysis/Tenant/BLK-CC-01-report.md` | Renamed (moved into Tenant subfolder) |
| `apps/services/careconnect/CareConnect.Api/Endpoints/ProviderOnboardingEndpoints.cs` | Updated — resume-aware message, `IsResumed` mapped to response |
| `apps/services/careconnect/CareConnect.Application/DTOs/ProviderOnboardingDtos.cs` | Updated — `ProviderOnboardingResponse` + `IsResumed` field |
| `apps/services/careconnect/CareConnect.Application/Interfaces/IProviderOnboardingService.cs` | Updated — `ProviderOnboardingResult` + `IsResumed` field |
| `apps/services/careconnect/CareConnect.Application/Services/ProviderOnboardingService.cs` | Refactored — full two-save resumable flow (+188 lines) |
| `apps/services/careconnect/CareConnect.Domain/Provider.cs` | Updated — 6 recovery state fields + `TenantOnboardingStatuses` class + 4 domain methods (+89 lines) |
| `apps/services/careconnect/CareConnect.Infrastructure/Data/Configurations/ProviderConfiguration.cs` | Updated — EF mappings for 6 new fields + 2 indexes (+15 lines) |
| `apps/services/careconnect/CareConnect.Infrastructure/Data/Migrations/20260423230000_AddProviderOnboardingRecoveryState.cs` | New — EF migration (6 columns, 2 indexes, Up + Down) |
| `apps/services/careconnect/CareConnect.Infrastructure/Data/Migrations/CareConnectDbContextModelSnapshot.cs` | Updated — snapshot brought current (AccessStage, Npi, IdentityUserId, all recovery state fields + indexes) |
| `attached_assets/BLK-CC-02-CareConnect-Onboarding-R_1776971778584.txt` | Source brief (instructions) |

---

## Key Changes

### Provider domain (`CareConnect.Domain/Provider.cs`)

New constants class:
- `TenantOnboardingStatuses` — `None | ProvisioningStarted | TenantProvisioned | Completed | Failed`

New properties:
- `PendingTenantId` (`Guid?`) — TenantId from Tenant service; retained until both steps succeed
- `PendingTenantCode` (`string?`, maxLength 100)
- `PendingTenantSubdomain` (`string?`, maxLength 200)
- `TenantOnboardingStatus` (`string`, NOT NULL, default `"None"`, maxLength 30)
- `LastOnboardingError` (`string?`, maxLength 500)
- `LastOnboardingAttemptAtUtc` (`DateTime?`)

New domain methods:
- `BeginOnboarding()` — records attempt timestamp, sets `ProvisioningStarted`
- `RecordTenantProvisioned(tenantId, code, subdomain)` — writes pending state, sets `TenantProvisioned`
- `CompleteOnboarding(providerTenantId)` — calls `MarkTenantProvisioned`, clears pending, sets `Completed`
- `RecordOnboardingFailure(error)` — stores error, sets `Failed`, keeps pending state for retry

### ProviderOnboardingService (`...Application/Services/ProviderOnboardingService.cs`)

- **Two-save pattern**: DB save after Tenant provision (pending state) + DB save after Identity success (completion)
- **Resume path**: if `PendingTenantId != null`, skips `_tenantClient.ProvisionAsync` entirely; reuses stored pending data
- **Fresh path**: calls Tenant service; on success saves pending state before Identity call
- **Identity failure handling**: saves failure state + retains pending state; next retry resumes cleanly
- **Result**: `IsResumed=true` on resume path, `IsResumed=false` on fresh path

### Migration (`20260423230000_AddProviderOnboardingRecoveryState.cs`)

Six new columns on `cc_Providers`:
- `PendingTenantId` `char(36)` nullable
- `PendingTenantCode` `varchar(100)` nullable
- `PendingTenantSubdomain` `varchar(200)` nullable
- `TenantOnboardingStatus` `varchar(30)` NOT NULL DEFAULT `'None'`
- `LastOnboardingError` `varchar(500)` nullable
- `LastOnboardingAttemptAtUtc` `datetime(6)` nullable

Two new indexes: `IX_Providers_TenantOnboardingStatus`, `IX_Providers_PendingTenantId`

### Model snapshot (`CareConnectDbContextModelSnapshot.cs`)

Snapshot was behind by 3 prior migrations. Brought fully current:
- Added: `AccessStage` (default `"URL"`), `Npi`, `IdentityUserId`, `CommonPortalActivatedAtUtc`, `TenantProvisionedAtUtc`
- Added: all 6 BLK-CC-02 recovery state fields
- Added: corresponding indexes for all new fields

### DTO / Interface updates

- `ProviderOnboardingResult.IsResumed` (bool) — returned from service layer
- `ProviderOnboardingResponse.IsResumed` (bool) — serialized in HTTP response
- Endpoint message: `"We found an existing workspace setup. Setup is now complete."` on resume

---

## Diff File

`analysis/BLK-CC-02-commit.diff.txt` — 59,577 bytes / 1,315 lines
