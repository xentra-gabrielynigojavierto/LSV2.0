# Legacy Migration Notes — SynqLiens-Core → LegalSynq v2.0

This document captures the migration commentary from the lookup seed migration
(`20260513000001_SeedLookupValues.cs`) that transferred reference data from the
legacy **SynqLiens-Core** (FluentMigrator) project into the v2.0 EF Core schema.

---

## Overview

All legacy lookup/reference tables were consolidated into a single
`liens_LookupValues` table, partitioned by a `Category` column.
Every row is tenant-agnostic (`TenantId = NULL`) and flagged `IsSystem = 1`.

---

## Category Map

| Category | Legacy Table | Legacy Migration(s) | Notes |
|---|---|---|---|
| `State` | `STATES` | `_00003CreateStates` / `_00004InsertStates` | Column rename: `STATES_CODE → Code`, `STATES_DESCRIPTION → Name` |
| `ContactType` | `SL_CONTACT_TYPE` | `_00006CreateContactType` / `_00007InsertContactType` | Legacy had 3 codes (LF/MP/FC); extended with v2.0 ContactType enum values |
| `AccidentType` | `SL_ACCIDENT_TYPE` | `_00008CreateAccidentType` / `_00038InsertAccidentType` | Column rename: `AT_CODE → Code`, `AT_DESCRIPTION → Name` |
| `DocumentCategory` | `SL_DOCUMENT_TYPE` | `_00031CreateDocumentType` / `_00074LienAgreementDocType` | Column rename: `DT_CODE → Code`, `DT_DESCRIPTION → Name` |
| `LienStatus` | `SL_LIENS_STATUS` | `_00032CreateLiensStatus` / `_00033InsertLiensStatus` | Legacy had only Open/Close; extended with v2.0 LienStatus domain enum |
| `LienType` | *(inline enum)* | — | New in v2.0; sourced from `LienType` domain enum |
| `CaseStatus` | *(inline enum)* | — | New in v2.0; sourced from `CaseStatus` domain enum |
| `ServicingStatus` | *(inline enum)* | — | New in v2.0; sourced from `ServicingStatus` domain enum |
| `ServicingPriority` | *(inline enum)* | — | New in v2.0; sourced from `ServicingPriority` domain enum |

---

## Per-Category Migration Notes

### State
- **Source:** `_00003CreateStates` / `_00004InsertStates`
- **Renamed:** `STATES` → `liens_LookupValues` (`Category = 'State'`)
- All 51 US states and DC seeded with two-letter USPS codes as `Code` and full name as `Name`.

### ContactType
- **Source:** `_00006CreateContactType` / `_00007InsertContactType` (LF/MP/FC)
- **Extended with v2.0 ContactType enum values.**
- **Renamed:** `SL_CONTACT_TYPE` → `liens_LookupValues` (`Category = 'ContactType'`)
- Legacy codes LF (Law Firm), MP (Medical Provider), FC (Funding Company) replaced with
  PascalCase codes aligned to the v2.0 `ContactType` domain enum:
  `LawFirm`, `Provider`, `LienHolder`, `CaseManager`, `InternalUser`.

### AccidentType
- **Source:** `_00008CreateAccidentType` / `_00038InsertAccidentType`
- **Renamed:** `SL_ACCIDENT_TYPE` → `liens_LookupValues` (`Category = 'AccidentType'`)
- Codes normalized to PascalCase (e.g. `Motor Vehicle Accident` → `MotorVehicleAccident`).
- 38 accident types seeded.

### DocumentCategory
- **Source:** `_00031CreateDocumentType` / `_00074LienAgreementDocType`
- **Renamed:** `SL_DOCUMENT_TYPE` → `liens_LookupValues` (`Category = 'DocumentCategory'`)
- Expanded with additional document types present in the v2.0 workflow:
  `LienAgreement`, `MedicalRecord`, `DemandLetter`, `SettlementAgreement`,
  `BillOfSale`, `InsuranceDocument`, `AttorneyContract`, `CourtFiling`,
  `PayoffStatement`, `CheckDocument`, `Other`.

### LienStatus
- **Source:** `_00032CreateLiensStatus` / `_00033InsertLiensStatus` (Open/Close)
- **Extended with v2.0 LienStatus domain enum values.**
- **Renamed:** `SL_LIENS_STATUS` → `liens_LookupValues` (`Category = 'LienStatus'`)
- Legacy had only two statuses (Open/Close); replaced with the full v2.0 lifecycle:
  `Draft`, `Offered`, `UnderReview`, `Sold`, `Active`, `Settled`, `Withdrawn`,
  `Cancelled`, `Disputed`.

### LienType
- **Source:** v2.0 `LienType` domain enum (no legacy equivalent)
- **Renamed:** (inline enum) → `liens_LookupValues` (`Category = 'LienType'`)
- Values: `MedicalLien`, `AttorneyLien`, `SettlementAdvance`, `WorkersCompLien`,
  `PropertyLien`, `Other`.

### CaseStatus
- **Source:** v2.0 `CaseStatus` domain enum (no legacy equivalent)
- **Renamed:** (inline enum) → `liens_LookupValues` (`Category = 'CaseStatus'`)
- Values: `PreDemand`, `DemandSent`, `InNegotiation`, `CaseSettled`, `Closed`.

### ServicingStatus
- **Source:** v2.0 `ServicingStatus` domain enum (no legacy equivalent)
- Values: `Pending`, `InProgress`, `Completed`, `Escalated`, `OnHold`.

### ServicingPriority
- **Source:** v2.0 `ServicingPriority` domain enum (no legacy equivalent)
- Values: `Low`, `Normal`, `High`, `Urgent`.

---

## Rollback

`Down()` deletes all rows where `IsSystem = 1 AND TenantId IS NULL` for the
categories listed above. No tenant-specific data is affected.

---

## API Endpoints

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/liens/lookups/categories` | List all valid category names |
| `GET` | `/api/liens/lookups/all` | All categories with their values |
| `GET` | `/api/liens/lookups/{category}` | Values for a specific category |
| `GET` | `/api/liens/lookups/{category}/{code}` | Single lookup value by category + code |

All endpoints require `SYNQ_LIENS` product access and `lien:read` permission.
