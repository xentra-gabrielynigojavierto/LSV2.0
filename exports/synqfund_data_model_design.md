# SynqFund — Multi-Organization, Multi-Party Data Model Design

**Service:** `Fund` (port 5002)  
**Database:** `fund_db` on AWS RDS MySQL 8.0  
**Platform foundation:** Tenant → Organization → Party → User  
**Date:** 2026-03-28

---

## 1. Application Core Schema

### Design

The `Applications` table is the central record of a funding lifecycle event. It links three platform actors — an injured party (applicant), a referring law firm, and a receiving funder — without assuming those actors share a tenant.

```sql
CREATE TABLE `Applications` (
    -- Identity
    `Id`                        char(36)        NOT NULL,
    `ApplicationNumber`         varchar(50)     NOT NULL,

    -- Actor linkages (the access model)
    `ApplicantPartyId`          char(36)        NOT NULL,   -- Party.Id in careconnect_db or identity context
    `ReferringOrganizationId`   char(36)        NULL,       -- LAW_FIRM org that submitted / sponsored
    `ReceivingOrganizationId`   char(36)        NULL,       -- FUNDER org assigned to review

    -- Partition key only (subdomain/account boundary)
    `TenantId`                  char(36)        NOT NULL,

    -- Snapshot fields (kept for read performance; source of truth is the Party record)
    `ApplicantFirstName`        varchar(100)    NOT NULL,
    `ApplicantLastName`         varchar(100)    NOT NULL,
    `Email`                     varchar(320)    NOT NULL,
    `Phone`                     varchar(30)     NOT NULL,

    -- Financials
    `RequestedAmount`           decimal(18,2)   NULL,
    `ApprovedAmount`            decimal(18,2)   NULL,
    `CurrencyCode`              char(3)         NOT NULL DEFAULT 'USD',

    -- Workflow
    `Status`                    varchar(30)     NOT NULL DEFAULT 'Draft',
    `CaseType`                  varchar(50)     NULL,       -- e.g. PersonalInjury, WorkersComp
    `IncidentDate`              date            NULL,
    `AttorneyNotes`             varchar(2000)   NULL,

    -- Audit
    `CreatedByUserId`           char(36)        NULL,
    `UpdatedByUserId`           char(36)        NULL,
    `CreatedAtUtc`              datetime(6)     NOT NULL,
    `UpdatedAtUtc`              datetime(6)     NOT NULL,

    CONSTRAINT `PK_Applications` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

-- Unique per tenant
CREATE UNIQUE INDEX `IX_Applications_TenantId_ApplicationNumber`
    ON `Applications` (`TenantId`, `ApplicationNumber`);

-- Law firm query: "show me all apps I referred"
CREATE INDEX `IX_Applications_ReferringOrgId_Status`
    ON `Applications` (`ReferringOrganizationId`, `Status`);

-- Funder query: "show me all apps assigned to me"
CREATE INDEX `IX_Applications_ReceivingOrgId_Status`
    ON `Applications` (`ReceivingOrganizationId`, `Status`);

-- Party portal query: "show me my own applications"
CREATE INDEX `IX_Applications_ApplicantPartyId`
    ON `Applications` (`ApplicantPartyId`);

-- Admin / reporting query
CREATE INDEX `IX_Applications_TenantId_Status_CreatedAt`
    ON `Applications` (`TenantId`, `Status`, `CreatedAtUtc`);
```

### Why `TenantId` Is NOT the Access Boundary

`TenantId` is a **partitioning key**, not an access control key. It identifies which subdomain/account owns the record at the storage level.

Access control in SynqFund is **organization-based**:

| Actor | Access predicate |
|---|---|
| Law firm user | `ReferringOrganizationId = @orgId` |
| Funder user | `ReceivingOrganizationId = @orgId` |
| Injured party | `ApplicantPartyId = @partyId` |

A law firm in Tenant A and a funder in Tenant B can both act on the same `Application` record because neither query filters by `TenantId`. The record lives in whichever tenant created it, but the row is visible to any authorized org regardless of their own `TenantId`.

Filtering by `TenantId` alone would break cross-organization collaboration — the core value proposition of SynqFund.

---

## 2. Application Status Model

### Full Lifecycle

```
Draft ──► Submitted ──► UnderReview ──► Approved ──► Funded
                │                   │
                │                   └──► Denied
                │
                └──► Withdrawn
```

| Status | Owned by | Description |
|---|---|---|
| `Draft` | Law Firm | Application created but not yet sent to a funder. Editable. |
| `Submitted` | Law Firm | Law firm locked the record and transmitted it to the assigned funder. |
| `UnderReview` | Funder | Funder acknowledged receipt; reviewing documentation. |
| `Approved` | Funder | Funder approved funding up to `ApprovedAmount`. |
| `Denied` | Funder | Funder declined; `ApprovedAmount` remains null. |
| `Funded` | Funder | Disbursement(s) confirmed; money sent to applicant or law firm trust. |
| `Withdrawn` | Law Firm | Law firm cancelled before funder decision. |

### Transition Rules

```
Draft        → Submitted  | actor: LAW_FIRM;   requires ReceivingOrganizationId set
Submitted    → UnderReview| actor: FUNDER;     acknowledgement only
Submitted    → Withdrawn  | actor: LAW_FIRM;   only before UnderReview
UnderReview  → Approved   | actor: FUNDER;     requires RequestedAmount, creates FundingDecision
UnderReview  → Denied     | actor: FUNDER;     requires denial reason in FundingDecision
Approved     → Funded     | actor: FUNDER;     requires at least one FundingDisbursement confirmed
```

Illegal transitions raise a domain exception. The current status is authoritative; status history is append-only.

### Auditability

Every status change writes an `ApplicationStatusHistories` row (see §3). The `Applications` table holds only the current status and audit timestamps. Historical reconstruction is always available from the history table.

---

## 3. Supporting Tables

### 3.1 ApplicationStatusHistories

**Purpose:** Immutable append-only audit trail of every status transition. Answers "who moved this to Approved and when?"

```sql
CREATE TABLE `ApplicationStatusHistories` (
    `Id`                char(36)        NOT NULL,
    `ApplicationId`     char(36)        NOT NULL,
    `FromStatus`        varchar(30)     NOT NULL,
    `ToStatus`          varchar(30)     NOT NULL,
    `ChangedByUserId`   char(36)        NULL,
    `ChangedByOrgId`    char(36)        NULL,
    `Reason`            varchar(500)    NULL,
    `ChangedAtUtc`      datetime(6)     NOT NULL,
    CONSTRAINT `PK_ApplicationStatusHistories` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_StatusHistories_Applications`
        FOREIGN KEY (`ApplicationId`) REFERENCES `Applications` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_StatusHistories_ApplicationId_ChangedAt`
    ON `ApplicationStatusHistories` (`ApplicationId`, `ChangedAtUtc`);
```

### 3.2 ApplicationDocuments

**Purpose:** Track documents attached to an application (medical records, police reports, attorney LOI, etc.). Stores metadata only; file bytes live in object storage (S3/GCS).

```sql
CREATE TABLE `ApplicationDocuments` (
    `Id`                char(36)        NOT NULL,
    `ApplicationId`     char(36)        NOT NULL,
    `DocumentType`      varchar(50)     NOT NULL,   -- MedicalRecord, PoliceReport, LienLetter, etc.
    `FileName`          varchar(255)    NOT NULL,
    `StorageKey`        varchar(500)    NOT NULL,   -- S3 object key
    `ContentType`       varchar(100)    NOT NULL,
    `FileSizeBytes`     bigint          NOT NULL DEFAULT 0,
    `UploadedByUserId`  char(36)        NULL,
    `UploadedByOrgId`   char(36)        NULL,
    `VisibilityScope`   varchar(20)     NOT NULL DEFAULT 'SHARED', -- SHARED | FUNDER_ONLY | LAW_FIRM_ONLY
    `IsDeleted`         tinyint(1)      NOT NULL DEFAULT 0,
    `UploadedAtUtc`     datetime(6)     NOT NULL,
    CONSTRAINT `PK_ApplicationDocuments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Documents_Applications`
        FOREIGN KEY (`ApplicationId`) REFERENCES `Applications` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_Documents_ApplicationId_Type`
    ON `ApplicationDocuments` (`ApplicationId`, `DocumentType`);
```

### 3.3 FundingDecisions

**Purpose:** Records the formal approval or denial by the funder, including the approved amount, rate, and fee structure. One application has at most one active decision (a re-review after denial creates a new record and marks the prior as superseded).

```sql
CREATE TABLE `FundingDecisions` (
    `Id`                    char(36)        NOT NULL,
    `ApplicationId`         char(36)        NOT NULL,
    `DecidingOrgId`         char(36)        NOT NULL,   -- ReceivingOrganizationId
    `DecidedByUserId`       char(36)        NULL,
    `Decision`              varchar(20)     NOT NULL,   -- Approved | Denied
    `ApprovedAmount`        decimal(18,2)   NULL,
    `InterestRatePct`       decimal(7,4)    NULL,       -- annual %
    `OriginationFeePct`     decimal(7,4)    NULL,
    `MaxRepaymentAmount`    decimal(18,2)   NULL,
    `Reason`                varchar(1000)   NULL,
    `IsSuperseded`          tinyint(1)      NOT NULL DEFAULT 0,
    `DecidedAtUtc`          datetime(6)     NOT NULL,
    CONSTRAINT `PK_FundingDecisions` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Decisions_Applications`
        FOREIGN KEY (`ApplicationId`) REFERENCES `Applications` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_FundingDecisions_ApplicationId`
    ON `FundingDecisions` (`ApplicationId`);
CREATE INDEX `IX_FundingDecisions_DecidingOrgId_Decision`
    ON `FundingDecisions` (`DecidingOrgId`, `Decision`);
```

### 3.4 FundingDisbursements

**Purpose:** Each money movement event after approval. An approval may disburse in multiple tranches (e.g., medical funding now, living expenses later). Tracks method, confirmation reference, and timestamps separately from the decision.

```sql
CREATE TABLE `FundingDisbursements` (
    `Id`                    char(36)        NOT NULL,
    `ApplicationId`         char(36)        NOT NULL,
    `FundingDecisionId`     char(36)        NOT NULL,
    `DisbursedByOrgId`      char(36)        NOT NULL,
    `DisbursedByUserId`     char(36)        NULL,
    `Amount`                decimal(18,2)   NOT NULL,
    `CurrencyCode`          char(3)         NOT NULL DEFAULT 'USD',
    `DisbursementMethod`    varchar(30)     NOT NULL,   -- ACH | Wire | Check | Other
    `RecipientType`         varchar(30)     NOT NULL,   -- Party | LawFirmTrust | Provider
    `RecipientRef`          varchar(200)    NULL,       -- account/routing or check number
    `ConfirmationRef`       varchar(200)    NULL,       -- wire ref or ACH trace
    `Status`                varchar(20)     NOT NULL DEFAULT 'Pending', -- Pending | Sent | Confirmed | Failed
    `Notes`                 varchar(500)    NULL,
    `ScheduledAtUtc`        datetime(6)     NULL,
    `SentAtUtc`             datetime(6)     NULL,
    `ConfirmedAtUtc`        datetime(6)     NULL,
    `CreatedAtUtc`          datetime(6)     NOT NULL,
    CONSTRAINT `PK_FundingDisbursements` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Disbursements_Applications`
        FOREIGN KEY (`ApplicationId`) REFERENCES `Applications` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Disbursements_Decisions`
        FOREIGN KEY (`FundingDecisionId`) REFERENCES `FundingDecisions` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_Disbursements_ApplicationId`
    ON `FundingDisbursements` (`ApplicationId`);
CREATE INDEX `IX_Disbursements_DecisionId`
    ON `FundingDisbursements` (`FundingDecisionId`);
CREATE INDEX `IX_Disbursements_DisbursedByOrgId_Status`
    ON `FundingDisbursements` (`DisbursedByOrgId`, `Status`);
```

---

## 4. Party Integration

### How `ApplicantPartyId` Is Used

`ApplicantPartyId` is the durable cross-service identity of the injured person. The `Party` record lives in `careconnect_db` (CareConnect service); the Fund service stores only the foreign key. On read, the API can call CareConnect's internal Party endpoint to hydrate name/dob/contact details, or rely on the snapshot columns (`ApplicantFirstName`, `ApplicantLastName`, `Email`, `Phone`) for list views without a round-trip.

```
JWT claim: party_id (set when applicant logs in via Party portal)
                │
                ▼
GET /api/applications?myApplications=true
  → WHERE ApplicantPartyId = @partyId   (from JWT)
  → No org filter applied
```

The `ApplicantPartyId` is set at application creation time. It cannot be changed after the record is submitted (status ≥ `Submitted`).

### Party Portal Access Rules

The Party (injured applicant) is authenticated via a dedicated identity flow that issues a JWT with a `party_id` claim instead of `org_id`. The Party portal enforces a strict read-only rule:

| Operation | Allowed | Filter |
|---|---|---|
| List own applications | Yes | `ApplicantPartyId = @partyId` |
| View application detail | Yes | `ApplicantPartyId = @partyId AND Id = @id` |
| View funding decision | Yes (approved/funded only) | Joined to `FundingDecisions` |
| View disbursements | Yes | Joined to `FundingDisbursements` |
| Create application | No | Party cannot self-apply; law firm creates on their behalf |
| Update application | No | Read-only portal |
| View documents | Scoped | `VisibilityScope != 'FUNDER_ONLY'` |
| View attorney notes | No | `AttorneyNotes` is law-firm-internal |

---

## 5. EF Core Design

### Entity Classes

```csharp
// Fund.Domain/Application.cs (updated)
public class Application : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ApplicationNumber { get; private set; } = string.Empty;

    // Actor linkages
    public Guid ApplicantPartyId { get; private set; }
    public Guid? ReferringOrganizationId { get; private set; }
    public Guid? ReceivingOrganizationId { get; private set; }

    // Snapshot (denormalized for performance)
    public string ApplicantFirstName { get; private set; } = string.Empty;
    public string ApplicantLastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;

    // Financials
    public decimal? RequestedAmount { get; private set; }
    public decimal? ApprovedAmount { get; private set; }
    public string CurrencyCode { get; private set; } = "USD";

    // Workflow
    public string Status { get; private set; } = "Draft";
    public string? CaseType { get; private set; }
    public DateOnly? IncidentDate { get; private set; }
    public string? AttorneyNotes { get; private set; }

    // Navigation
    private readonly List<ApplicationStatusHistory> _statusHistories = new();
    private readonly List<ApplicationDocument> _documents = new();
    private readonly List<FundingDecision> _decisions = new();
    private readonly List<FundingDisbursement> _disbursements = new();

    public IReadOnlyCollection<ApplicationStatusHistory> StatusHistories => _statusHistories.AsReadOnly();
    public IReadOnlyCollection<ApplicationDocument> Documents => _documents.AsReadOnly();
    public IReadOnlyCollection<FundingDecision> Decisions => _decisions.AsReadOnly();
    public IReadOnlyCollection<FundingDisbursement> Disbursements => _disbursements.AsReadOnly();
}

// Fund.Domain/ApplicationStatusHistory.cs
public class ApplicationStatusHistory
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string FromStatus { get; private set; } = string.Empty;
    public string ToStatus { get; private set; } = string.Empty;
    public Guid? ChangedByUserId { get; private set; }
    public Guid? ChangedByOrgId { get; private set; }
    public string? Reason { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }

    public Application Application { get; private set; } = null!;

    public static ApplicationStatusHistory Create(
        Guid applicationId, string from, string to,
        Guid? userId, Guid? orgId, string? reason = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = userId,
            ChangedByOrgId = orgId,
            Reason = reason,
            ChangedAtUtc = DateTime.UtcNow
        };
}

// Fund.Domain/ApplicationDocument.cs
public class ApplicationDocument
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public Guid? UploadedByUserId { get; private set; }
    public Guid? UploadedByOrgId { get; private set; }
    public string VisibilityScope { get; private set; } = "SHARED";
    public bool IsDeleted { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }

    public Application Application { get; private set; } = null!;
}

// Fund.Domain/FundingDecision.cs
public class FundingDecision
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid DecidingOrgId { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public string Decision { get; private set; } = string.Empty; // Approved | Denied
    public decimal? ApprovedAmount { get; private set; }
    public decimal? InterestRatePct { get; private set; }
    public decimal? OriginationFeePct { get; private set; }
    public decimal? MaxRepaymentAmount { get; private set; }
    public string? Reason { get; private set; }
    public bool IsSuperseded { get; private set; }
    public DateTime DecidedAtUtc { get; private set; }

    public Application Application { get; private set; } = null!;

    private readonly List<FundingDisbursement> _disbursements = new();
    public IReadOnlyCollection<FundingDisbursement> Disbursements => _disbursements.AsReadOnly();
}

// Fund.Domain/FundingDisbursement.cs
public class FundingDisbursement
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid FundingDecisionId { get; private set; }
    public Guid DisbursedByOrgId { get; private set; }
    public Guid? DisbursedByUserId { get; private set; }
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = "USD";
    public string DisbursementMethod { get; private set; } = string.Empty;
    public string RecipientType { get; private set; } = string.Empty;
    public string? RecipientRef { get; private set; }
    public string? ConfirmationRef { get; private set; }
    public string Status { get; private set; } = "Pending";
    public string? Notes { get; private set; }
    public DateTime? ScheduledAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Application Application { get; private set; } = null!;
    public FundingDecision FundingDecision { get; private set; } = null!;
}
```

### EF Relationships

```
Application (1) ──< ApplicationStatusHistories (many)   [cascade delete]
Application (1) ──< ApplicationDocuments       (many)   [cascade delete]
Application (1) ──< FundingDecisions           (many)   [cascade delete]
Application (1) ──< FundingDisbursements       (many)   [cascade delete]
FundingDecision (1) ──< FundingDisbursements   (many)   [restrict delete]
```

---

## 6. Migration Plan

### Current State
`Applications` table has: `Id`, `TenantId`, `ApplicationNumber`, `ApplicantFirstName`, `ApplicantLastName`, `Email`, `Phone`, `Status` (New/InReview/Approved/Rejected/Funded), `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`.

### Phase A — Add new columns to `Applications` (non-breaking)

**Migration `20260329200001_AddFundPartyAndOrgColumns`**

Add nullable columns: `ApplicantPartyId`, `ReferringOrganizationId`, `ReceivingOrganizationId`, `RequestedAmount`, `ApprovedAmount`, `CurrencyCode`, `CaseType`, `IncidentDate`, `AttorneyNotes`. Use the `INFORMATION_SCHEMA` idempotent pattern.

All existing rows will have `ApplicantPartyId = NULL` after Phase A — backward compatible with the old service code.

### Phase B — Create supporting tables

**Migration `20260329200002_AddSupportingFundTables`**

Create `ApplicationStatusHistories`, `ApplicationDocuments`, `FundingDecisions`, `FundingDisbursements` with `CREATE TABLE IF NOT EXISTS`.

Backfill `ApplicationStatusHistories` for all existing rows: insert a synthetic `New → <current_status>` history row so every application has at least one history entry.

```sql
INSERT INTO `ApplicationStatusHistories`
    (`Id`, `ApplicationId`, `FromStatus`, `ToStatus`, `ChangedByUserId`,
     `ChangedByOrgId`, `Reason`, `ChangedAtUtc`)
SELECT UUID(), `Id`, 'New', `Status`, `CreatedByUserId`,
       NULL, 'Backfill from Phase A migration', `CreatedAtUtc`
FROM `Applications`
WHERE `Status` != 'New';
```

### Phase C — Status vocabulary alignment

**Migration `20260329200003_AlignApplicationStatus`**

Rename legacy status values to the new vocabulary:

| Old value | New value |
|---|---|
| `New` | `Draft` |
| `InReview` | `UnderReview` |
| `Approved` | `Approved` (unchanged) |
| `Rejected` | `Denied` |
| `Funded` | `Funded` (unchanged) |

```sql
UPDATE `Applications` SET `Status` = 'Draft'       WHERE `Status` = 'New';
UPDATE `Applications` SET `Status` = 'UnderReview'  WHERE `Status` = 'InReview';
UPDATE `Applications` SET `Status` = 'Denied'       WHERE `Status` = 'Rejected';
```

Update `Application.ValidStatuses` in the domain simultaneously with this migration deployment.

---

## 7. Authorization Rules

### JWT Claims Available

| Claim | Value example |
|---|---|
| `tenant_id` | `20000000-...-001` |
| `org_id` | `40000000-...-001` |
| `org_type` | `LAW_FIRM` / `FUNDER` / `PROVIDER` |
| `product_roles` | `["SYNQFUND_ATTORNEY", "SYNQFUND_REVIEWER"]` |
| `party_id` | set only for Party portal logins |

### Law Firm (`org_type = LAW_FIRM`)

```csharp
// Create application
// POST /api/applications
// - org_type must be LAW_FIRM
// - Sets ReferringOrganizationId = orgId from JWT
// - Status starts at Draft

// List own applications
// GET /api/applications
// - WHERE ReferringOrganizationId = orgId
// - All statuses visible

// Submit to funder
// POST /api/applications/{id}/submit
// - Validates org is the referring org
// - Sets ReceivingOrganizationId = request.FunderOrgId
// - Transitions Draft → Submitted
// - Writes status history

// Withdraw application
// POST /api/applications/{id}/withdraw
// - Only if Status IN ('Draft', 'Submitted')
// - Validates org is the referring org
```

### Funder (`org_type = FUNDER`)

```csharp
// List assigned applications
// GET /api/applications
// - WHERE ReceivingOrganizationId = orgId
// - Statuses: Submitted, UnderReview, Approved, Denied, Funded

// Acknowledge / begin review
// POST /api/applications/{id}/begin-review
// - Validates ReceivingOrganizationId = orgId
// - Transitions Submitted → UnderReview

// Approve
// POST /api/applications/{id}/approve
// - Validates ReceivingOrganizationId = orgId
// - Creates FundingDecision { Decision = "Approved", ApprovedAmount, ... }
// - Transitions UnderReview → Approved

// Deny
// POST /api/applications/{id}/deny
// - Validates ReceivingOrganizationId = orgId
// - Creates FundingDecision { Decision = "Denied", Reason }
// - Transitions UnderReview → Denied

// Record disbursement
// POST /api/applications/{id}/disburse
// - Validates Status = Approved
// - Validates ReceivingOrganizationId = orgId
// - Creates FundingDisbursement
// - If all disbursements confirmed → transitions Approved → Funded
```

### Injured Party (`party_id` claim present)

```csharp
// List own applications
// GET /api/applications/mine
// - WHERE ApplicantPartyId = partyId  (from JWT party_id claim)
// - Read-only; no create/update/delete

// Get application detail
// GET /api/applications/{id}/party-view
// - WHERE ApplicantPartyId = partyId AND Id = id
// - Returns: status, approvedAmount, disbursements (not AttorneyNotes, not FUNDER_ONLY docs)
```

### Endpoint-level enforcement example

```csharp
app.MapPost("/api/applications/{id}/approve", async (
    Guid id,
    ApproveApplicationRequest req,
    ICurrentRequestContext ctx,
    IApplicationService svc,
    CancellationToken ct) =>
{
    if (ctx.OrgType != "FUNDER")
        return Results.Forbid();

    var result = await svc.ApproveAsync(id, ctx.OrgId, ctx.UserId, req, ct);
    return Results.Ok(result);
})
.RequireAuthorization("SynqFundReviewer");
```

---

## 8. Cross-Tenant Access Model

### The Problem

A law firm on tenant `legalsynq.lawfirm-alpha.com` submits an application to a funder on tenant `legalsynq.capitalfund.com`. The `Application` row lives in `fund_db` under `TenantId = lawfirm-alpha-tenant-id`. If the funder queries `WHERE TenantId = their-own-tenant-id`, they see nothing.

### The Solution: Organization-Scoped Queries

All cross-tenant reads use `OrganizationId` as the filter, never `TenantId`:

```sql
-- Law firm query (their own referrals, any tenant)
SELECT * FROM Applications
WHERE ReferringOrganizationId = @orgId
  AND Status != 'Draft'   -- optional; hide drafts from funder view
ORDER BY CreatedAtUtc DESC;

-- Funder query (all apps sent to them, any tenant)
SELECT * FROM Applications
WHERE ReceivingOrganizationId = @orgId
ORDER BY CreatedAtUtc DESC;

-- Party query (own applications, any tenant)
SELECT * FROM Applications
WHERE ApplicantPartyId = @partyId;
```

### TenantId Usage in Cross-Tenant Context

`TenantId` is still written to every row (for compliance partitioning, storage reporting, and tenant-scoped admin queries). It is NEVER used as an access filter in the application service layer. The API routes that serve law firms and funders must not accept `TenantId` as a query parameter.

### Funder Discovery

Before submission, the law firm must select a funder. The platform will expose an org directory endpoint (Identity service) listing active `FUNDER` organizations. The selected funder's `Id` becomes `ReceivingOrganizationId`. This is the only coordination step across tenants.

---

## 9. Funding Lifecycle

### State Transitions with Constraints

```
1. DRAFT
   - Created by law firm
   - Can set RequestedAmount, CaseType, IncidentDate
   - Documents uploadable (LAW_FIRM_ONLY by default)
   - No funder has visibility yet

2. SUBMITTED  [Law firm: POST /submit]
   - ReceivingOrganizationId must be set
   - RequestedAmount must be set
   - ApplicantPartyId must be set (party identity confirmed)
   - At least one document of type MedicalRecord or PoliceReport required
   - Funder gains read access

3. UNDER_REVIEW  [Funder: POST /begin-review]
   - Funder can request additional documents
   - Funder can add FUNDER_ONLY documents (internal analysis)
   - No edits by law firm allowed

4. APPROVED  [Funder: POST /approve]
   - Creates FundingDecision {Decision=Approved, ApprovedAmount, InterestRatePct, ...}
   - Sets Application.ApprovedAmount
   - Law firm and party notified (via event/notification service)

5. DENIED  [Funder: POST /deny]
   - Creates FundingDecision {Decision=Denied, Reason}
   - Law firm may re-submit to a different funder (creates new Application)
   - Original application remains as historical record

6. FUNDED  [Funder: POST /disburse × N]
   - One or more FundingDisbursements created
   - Each tracks its own Status: Pending → Sent → Confirmed
   - Application transitions to Funded when all disbursements are Confirmed
     OR when total Confirmed amount >= ApprovedAmount (whichever comes first)
   - Final status is terminal; no further transitions
```

### Disbursement Constraints

- `Amount` per disbursement must be > 0
- Sum of all `Confirmed` disbursements must not exceed `ApprovedAmount`
- A new disbursement can only be created if `Status IN ('Pending', 'Sent')` count < configured max tranches
- `ConfirmationRef` is required before a disbursement can move from `Sent → Confirmed`

---

## 10. Risks and Edge Cases

### 10.1 Duplicate Applications

**Risk:** A law firm submits two applications for the same injured party to the same or different funders.

**Mitigation:**
- No database unique constraint on `ApplicantPartyId` alone — legitimate to have multiple applications (different case types, different amounts, re-submission after denial).
- Service layer warns if an active application (`Status NOT IN ('Denied', 'Withdrawn')`) already exists for the same `ApplicantPartyId + ReferringOrganizationId`.
- The ApplicationNumber unique constraint (`TenantId + ApplicationNumber`) prevents exact duplicates from the same tenant.
- Admin UI should surface a "duplicate alert" badge for party-level collision.

### 10.2 Partial Funding

**Risk:** Funder approves $10,000 but disbursements only total $7,000.

**Mitigation:**
- `FundingDisbursements.Status` tracks each tranche independently.
- Application remains `Approved` (not `Funded`) until total confirmed disbursements reach `ApprovedAmount` or the funder explicitly closes the funding.
- Add a `POST /api/applications/{id}/close-funding` endpoint for funders to finalize with a partial amount, writing a `FundingDecision` with a `ClosedPartialAmount` note.

### 10.3 Multiple Funders

**Risk:** Law firm wants to split the application across two funders (co-funding).

**Mitigation (current model):** Not supported in Phase 1. One application has exactly one `ReceivingOrganizationId`. Co-funding requires the law firm to create separate applications (one per funder) with different `RequestedAmount` values that sum to the total need. Each application follows its own lifecycle independently.

**Future path:** A `CoFundingGroupId` column can link sibling applications in Phase 2 without breaking the current schema.

### 10.4 Party Identity Mismatch

**Risk:** Law firm creates an application with inline name/email that doesn't match the actual Party record in CareConnect.

**Mitigation:**
- At submission time (`Draft → Submitted`), the service calls CareConnect's Party read endpoint to validate `ApplicantPartyId` exists and the snapshot name matches within a configurable tolerance (e.g., levenshtein distance < 3, or exact DOB match).
- If mismatch is detected, submission is blocked with a 422 error listing the discrepant fields.
- `ApplicantPartyId` is locked (immutable) once `Status >= Submitted`.

### 10.5 Funder Organization Deactivated Mid-Review

**Risk:** A funder org is deactivated while applications are in `UnderReview`.

**Mitigation:**
- Applications remain in their current status.
- A nightly job (or event-driven handler) detects `ReceivingOrganizationId` pointing to an inactive org and transitions affected applications to `Submitted` with a status history entry noting the reassignment reason.
- Law firm is notified to select a new funder.

### 10.6 Party Portal Data Exposure

**Risk:** A party logs in and queries another party's application by guessing an ID.

**Mitigation:**
- All party-portal endpoints filter `WHERE ApplicantPartyId = @partyIdFromJwt AND Id = @id`. The `party_id` claim is non-forgeable (signed JWT).
- `AttorneyNotes` and `FUNDER_ONLY` documents are excluded at the query layer, not filtered client-side.
- Row-level: if `ApplicantPartyId` doesn't match, return HTTP 404 (not 403) to avoid confirming the ID exists.

### 10.7 Status Regression Attacks

**Risk:** A funder API caller attempts to transition a `Funded` application back to `Approved` to disburse again.

**Mitigation:**
- Domain `Application.TransitionStatus(to, actor)` method enforces the allowed transition matrix (see §2) and throws `InvalidOperationException` for illegal moves.
- The transition matrix is tested as a unit test suite.
- `Funded` and `Withdrawn` are terminal states with no valid outbound transitions.

---

*Document status: Design complete. Implementation sequencing: Phase A migration → Phase B migration → Phase C status rename → domain entity + EF config updates → Application service refactor → new endpoints (submit, approve, deny, disburse).*
