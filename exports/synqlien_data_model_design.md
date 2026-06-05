# SynqLien — Multi-Organization, Multi-Party Data Model Design

**Product:** SynqLien (future microservice, port 5004)  
**Database:** `lien_db` on AWS RDS MySQL 8.0  
**Platform foundation:** Tenant → Organization → Party → User  
**Date:** 2026-03-28

### Platform Role Mapping (corrected in migration `20260329000003`)

| Product Role | Org Type | Capabilities |
|---|---|---|
| `SYNQLIEN_SELLER` | `PROVIDER` | `lien:create`, `lien:offer`, `lien:read:own` |
| `SYNQLIEN_BUYER` | `LIEN_OWNER` | `lien:browse`, `lien:purchase`, `lien:read:held` |
| `SYNQLIEN_HOLDER` | `LIEN_OWNER` | `lien:read:held`, `lien:service`, `lien:settle` |

A single organization may hold both `SYNQLIEN_BUYER` and `SYNQLIEN_HOLDER` roles simultaneously. A provider cannot be a buyer or holder of the same lien it sells.

---

## 1. Lien Core Schema

### Design

The `Liens` table represents a single receivable obligation — typically a medical lien issued by a healthcare provider against a future settlement or judgment — that may be sold to, purchased by, and serviced by lien-owner organizations.

```sql
CREATE TABLE `Liens` (
    -- Identity
    `Id`                        char(36)        NOT NULL,
    `LienNumber`                varchar(50)     NOT NULL,   -- human-readable ref, unique per tenant

    -- Actor linkages (the access model — not tenant-based)
    `SellingOrganizationId`     char(36)        NOT NULL,   -- PROVIDER org that created the lien
    `BuyingOrganizationId`      char(36)        NULL,       -- LIEN_OWNER that purchased it (null until purchased)
    `HoldingOrganizationId`     char(36)        NULL,       -- LIEN_OWNER currently servicing (may differ from buyer)

    -- Subject linkage (optional — set when lien is tied to a known party/case)
    `SubjectPartyId`            char(36)        NULL,       -- Party.Id (CareConnect or shared registry)

    -- Partition key only
    `TenantId`                  char(36)        NOT NULL,

    -- Lien details
    `LienType`                  varchar(50)     NOT NULL,   -- MedicalLien, PropertyLien, AttorneyLien
    `OriginalAmount`            decimal(18,2)   NOT NULL,   -- face value of the lien
    `OfferPrice`                decimal(18,2)   NULL,       -- listed sale price (set when Offered)
    `PurchasePrice`             decimal(18,2)   NULL,       -- actual paid price (set when Purchased)
    `OutstandingBalance`        decimal(18,2)   NULL,       -- remaining balance during Active/settlement
    `CurrencyCode`              char(3)         NOT NULL DEFAULT 'USD',

    -- Case / claim context
    `CaseReference`             varchar(200)    NULL,       -- law firm case number or court docket
    `IncidentDate`              date            NULL,
    `ExpectedSettlementDate`    date            NULL,
    `JurisdictionState`         char(2)         NULL,

    -- Subject snapshot (denormalized for list performance; source of truth is Party record)
    `SubjectFirstName`          varchar(100)    NULL,
    `SubjectLastName`           varchar(100)    NULL,
    `SubjectDob`                date            NULL,

    -- Workflow
    `Status`                    varchar(30)     NOT NULL DEFAULT 'Draft',
    `IsConfidential`            tinyint(1)      NOT NULL DEFAULT 0, -- hides SubjectName from browse listings

    -- Audit
    `CreatedByUserId`           char(36)        NULL,
    `UpdatedByUserId`           char(36)        NULL,
    `CreatedAtUtc`              datetime(6)     NOT NULL,
    `UpdatedAtUtc`              datetime(6)     NOT NULL,

    CONSTRAINT `PK_Liens` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

-- Unique lien number within a tenant
CREATE UNIQUE INDEX `IX_Liens_TenantId_LienNumber`
    ON `Liens` (`TenantId`, `LienNumber`);

-- Provider query: "show me all liens I created"
CREATE INDEX `IX_Liens_SellingOrgId_Status`
    ON `Liens` (`SellingOrganizationId`, `Status`);

-- Buyer browse: "show me all offered liens available to purchase"
CREATE INDEX `IX_Liens_Status_LienType`
    ON `Liens` (`Status`, `LienType`);

-- Buyer/Holder query: "show me liens I own or service"
CREATE INDEX `IX_Liens_BuyingOrgId_Status`
    ON `Liens` (`BuyingOrganizationId`, `Status`);

CREATE INDEX `IX_Liens_HoldingOrgId_Status`
    ON `Liens` (`HoldingOrganizationId`, `Status`);

-- Party lookup: "show me all liens tied to this subject"
CREATE INDEX `IX_Liens_SubjectPartyId`
    ON `Liens` (`SubjectPartyId`);
```

### Why `TenantId` Is NOT the Access Boundary

`TenantId` is a **partitioning key** — it identifies which subdomain/account created the record for storage, compliance, and admin reporting purposes.

Access control in SynqLien is **organization-based**:

| Actor | Access predicate |
|---|---|
| Provider seller | `SellingOrganizationId = @orgId` |
| Lien owner (buyer) | `BuyingOrganizationId = @orgId` OR `Status = 'Offered'` (browse) |
| Lien owner (holder) | `HoldingOrganizationId = @orgId` |

A provider in Tenant A can list a lien, and a lien-owner firm in Tenant B can browse and purchase it. Neither side's `TenantId` is relevant to the other's query. Filtering by `TenantId` alone would make the marketplace invisible across org boundaries — destroying the product's value proposition.

---

## 2. Lien Status Model

### Full Lifecycle

```
Draft ──► Offered ──► UnderReview ──► Purchased ──► Active ──► Settled
  │                       │
  │                       └──► Offered  (buyer declines, put back on market)
  │
  └──► Cancelled  (seller withdraws before any purchase)
```

Extended paths:
```
Purchased ──► Active        (holder begins servicing)
Active    ──► Settled       (obligation fulfilled or case resolves)
Active    ──► Disputed      (settlement contested; returned to servicing after resolution)
Disputed  ──► Active        (dispute resolved)
Disputed  ──► Settled       (resolved in favor of settlement)
Purchased ──► Cancelled     (purchase rescinded within rescission window, if allowed)
```

### Transition Table

| From | To | Actor (org_type / role) | Condition |
|---|---|---|---|
| `Draft` | `Offered` | PROVIDER / SYNQLIEN_SELLER | `OriginalAmount` and `OfferPrice` set; at least one document attached |
| `Offered` | `UnderReview` | LIEN_OWNER / SYNQLIEN_BUYER | Buyer creates a `LienOffer` record (indication of interest) |
| `UnderReview` | `Offered` | LIEN_OWNER / SYNQLIEN_BUYER | Buyer declines; lien re-listed; `LienOffer` marked `Declined` |
| `UnderReview` | `Purchased` | LIEN_OWNER / SYNQLIEN_BUYER | Buyer confirms purchase; `LienPurchaseEvent` created |
| `Purchased` | `Active` | LIEN_OWNER / SYNQLIEN_HOLDER | Holder acknowledges assignment and begins servicing |
| `Active` | `Settled` | LIEN_OWNER / SYNQLIEN_HOLDER | `LienSettlementEvent` created; `OutstandingBalance = 0` |
| `Active` | `Disputed` | LIEN_OWNER / SYNQLIEN_HOLDER | Dispute raised against settlement |
| `Disputed` | `Active` | LIEN_OWNER / SYNQLIEN_HOLDER | Dispute resolved, servicing resumes |
| `Disputed` | `Settled` | LIEN_OWNER / SYNQLIEN_HOLDER | Settled during dispute resolution |
| `Draft` | `Cancelled` | PROVIDER / SYNQLIEN_SELLER | Seller withdraws before any purchase |
| `Offered` | `Cancelled` | PROVIDER / SYNQLIEN_SELLER | Seller withdraws from market |

### Auditability

Every status change writes an immutable `LienStatusHistories` row. The `Liens` table holds only the current status. Full history is always reconstructable from the history table.

---

## 3. Supporting Tables

### 3.1 LienStatusHistories

**Purpose:** Immutable append-only audit trail of every status transition. Answers "who moved this lien to Purchased and when, and from which organization?"

```sql
CREATE TABLE `LienStatusHistories` (
    `Id`                char(36)        NOT NULL,
    `LienId`            char(36)        NOT NULL,
    `FromStatus`        varchar(30)     NOT NULL,
    `ToStatus`          varchar(30)     NOT NULL,
    `ChangedByUserId`   char(36)        NULL,
    `ChangedByOrgId`    char(36)        NULL,
    `Reason`            varchar(500)    NULL,
    `ChangedAtUtc`      datetime(6)     NOT NULL,
    CONSTRAINT `PK_LienStatusHistories` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LienStatusHistories_Liens`
        FOREIGN KEY (`LienId`) REFERENCES `Liens` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_LienStatusHistories_LienId_ChangedAt`
    ON `LienStatusHistories` (`LienId`, `ChangedAtUtc`);
```

### 3.2 LienDocuments

**Purpose:** Metadata for files attached to a lien — the lien instrument itself, supporting medical records, chain-of-title documents, attorney letters, etc. File bytes live in object storage (S3). `VisibilityScope` controls cross-org access.

```sql
CREATE TABLE `LienDocuments` (
    `Id`                char(36)        NOT NULL,
    `LienId`            char(36)        NOT NULL,
    `DocumentType`      varchar(50)     NOT NULL,   -- LienInstrument, MedicalRecord, AssignmentDoc, etc.
    `FileName`          varchar(255)    NOT NULL,
    `StorageKey`        varchar(500)    NOT NULL,   -- S3 object key
    `ContentType`       varchar(100)    NOT NULL,
    `FileSizeBytes`     bigint          NOT NULL DEFAULT 0,
    `UploadedByUserId`  char(36)        NULL,
    `UploadedByOrgId`   char(36)        NULL,
    `VisibilityScope`   varchar(20)     NOT NULL DEFAULT 'SHARED',
    -- SHARED         = visible to seller, current buyer, and holder
    -- SELLER_ONLY    = visible only to SellingOrganizationId
    -- BUYER_ONLY     = visible only to BuyingOrganizationId / HoldingOrganizationId
    `IsDeleted`         tinyint(1)      NOT NULL DEFAULT 0,
    `UploadedAtUtc`     datetime(6)     NOT NULL,
    CONSTRAINT `PK_LienDocuments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LienDocuments_Liens`
        FOREIGN KEY (`LienId`) REFERENCES `Liens` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_LienDocuments_LienId_Type`
    ON `LienDocuments` (`LienId`, `DocumentType`);
```

### 3.3 LienOffers (Purchase Indication of Interest)

**Purpose:** Records each buyer's formal indication of interest in purchasing a lien. Allows tracking of multiple buyers who express interest in the same lien, their review status, any counter-offer amount, and the final decision. Only one `LienOffer` may be in `Accepted` state at a time per lien.

```sql
CREATE TABLE `LienOffers` (
    `Id`                    char(36)        NOT NULL,
    `LienId`                char(36)        NOT NULL,
    `BuyerOrgId`            char(36)        NOT NULL,
    `OfferedByUserId`       char(36)        NULL,
    `OfferedAmount`         decimal(18,2)   NULL,   -- buyer's counter-offer price (NULL = accepts asking price)
    `Status`                varchar(20)     NOT NULL DEFAULT 'Pending',
    -- Pending | Accepted | Declined | Withdrawn | Superseded
    `DecisionReason`        varchar(500)    NULL,
    `ExpiresAtUtc`          datetime(6)     NULL,   -- offer lapses if not accepted by this time
    `CreatedAtUtc`          datetime(6)     NOT NULL,
    `UpdatedAtUtc`          datetime(6)     NOT NULL,
    CONSTRAINT `PK_LienOffers` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LienOffers_Liens`
        FOREIGN KEY (`LienId`) REFERENCES `Liens` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_LienOffers_LienId_Status`
    ON `LienOffers` (`LienId`, `Status`);
CREATE INDEX `IX_LienOffers_BuyerOrgId_Status`
    ON `LienOffers` (`BuyerOrgId`, `Status`);
```

### 3.4 LienPurchaseEvents

**Purpose:** Immutable record of the completed purchase transaction. Created exactly once per lien when `Status` transitions to `Purchased`. Captures the final agreed price, the buyer org, and the effective date. Also records whether the buying org is acting as both buyer and holder (common) or whether a separate holder will be assigned.

```sql
CREATE TABLE `LienPurchaseEvents` (
    `Id`                    char(36)        NOT NULL,
    `LienId`                char(36)        NOT NULL,
    `LienOfferId`           char(36)        NULL,       -- the accepted LienOffer that led to this purchase
    `BuyerOrgId`            char(36)        NOT NULL,
    `HolderOrgId`           char(36)        NULL,       -- if null, BuyerOrg = HolderOrg at purchase time
    `PurchasedByUserId`     char(36)        NULL,
    `PurchasePrice`         decimal(18,2)   NOT NULL,
    `PurchaseMethod`        varchar(30)     NULL,       -- Wire | ACH | Check
    `PaymentConfirmRef`     varchar(200)    NULL,
    `EffectiveDate`         date            NOT NULL,
    `PurchasedAtUtc`        datetime(6)     NOT NULL,
    CONSTRAINT `PK_LienPurchaseEvents` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LienPurchaseEvents_Liens`
        FOREIGN KEY (`LienId`) REFERENCES `Liens` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE UNIQUE INDEX `IX_LienPurchaseEvents_LienId`
    ON `LienPurchaseEvents` (`LienId`);   -- enforces one purchase per lien
CREATE INDEX `IX_LienPurchaseEvents_BuyerOrgId`
    ON `LienPurchaseEvents` (`BuyerOrgId`);
```

### 3.5 LienSettlementEvents

**Purpose:** Records the resolution of the lien obligation — either through a case settlement, court judgment, payment-in-full, or write-off. Captures what was actually recovered vs. what was outstanding. Multiple partial payments can exist before full settlement; the final one sets `IsFinal = true` and triggers the `Settled` status transition.

```sql
CREATE TABLE `LienSettlementEvents` (
    `Id`                    char(36)        NOT NULL,
    `LienId`                char(36)        NOT NULL,
    `SettledByOrgId`        char(36)        NOT NULL,   -- HoldingOrganizationId at time of settlement
    `SettledByUserId`       char(36)        NULL,
    `SettlementType`        varchar(30)     NOT NULL,   -- CaseSettlement | CourtJudgment | PaymentInFull | WriteOff
    `AmountRecovered`       decimal(18,2)   NOT NULL,
    `BalanceBeforeEvent`    decimal(18,2)   NOT NULL,
    `BalanceAfterEvent`     decimal(18,2)   NOT NULL,
    `IsFinal`               tinyint(1)      NOT NULL DEFAULT 0,   -- true = triggers Settled status
    `SettlementRef`         varchar(200)    NULL,       -- case number, check number, court docket
    `Notes`                 varchar(1000)   NULL,
    `EffectiveDate`         date            NOT NULL,
    `RecordedAtUtc`         datetime(6)     NOT NULL,
    CONSTRAINT `PK_LienSettlementEvents` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LienSettlementEvents_Liens`
        FOREIGN KEY (`LienId`) REFERENCES `Liens` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_LienSettlementEvents_LienId_EffectiveDate`
    ON `LienSettlementEvents` (`LienId`, `EffectiveDate`);
CREATE INDEX `IX_LienSettlementEvents_SettledByOrgId`
    ON `LienSettlementEvents` (`SettledByOrgId`);
```

---

## 4. Party Integration

### When `SubjectPartyId` Is Needed

`SubjectPartyId` is set when the lien is directly tied to an identifiable injured person or patient whose case is tracked in CareConnect. This is the common scenario for medical liens arising from personal injury cases.

`SubjectPartyId` is **optional** because:
- Not all liens relate to a known platform party (e.g., a property lien has no party subject).
- A provider may list a lien before the patient has a Party record on the platform.
- The lien marketplace must function even when subject identity is not fully known.

### How Liens Relate to Party/Case Context

```
Party (CareConnect Parties table)
    │
    └── SubjectPartyId ──► Lien (Liens table)
                               │
                               └── LienDocuments (MedicalRecord, CaseRef, etc.)
```

When `SubjectPartyId` is set:
- The `SubjectFirstName`, `SubjectLastName`, `SubjectDob` snapshot columns are populated from the Party record at lien creation time.
- The CareConnect service can call the Lien service to retrieve all liens associated with a party (for referral/appointment context).
- If `IsConfidential = true`, the subject snapshot fields are **omitted** from the public browse listing (`Status = 'Offered'`) — buyers see lien financials and type but not the person's identity until they submit a `LienOffer`.

### Party Access Rules

The Party (subject) themselves **cannot directly access the Liens table**. Lien data is sensitive financial/legal information managed between provider and lien-owner organizations. The Party portal (in SynqFund or CareConnect) may surface a read-only summary derived from linked lien data, but the SynqLien service API does not expose a party-facing endpoint.

`SubjectPartyId` is **informational and linkage-only** — it is not part of the row-level access predicate. Access is always gated by `SellingOrganizationId`, `BuyingOrganizationId`, or `HoldingOrganizationId`.

---

## 5. EF Core Design

### Entity Classes

```csharp
// Fund.Domain/Lien.cs  (future: Lien.Domain/Lien.cs)
public class Lien : AuditableEntity
{
    public Guid Id { get; private set; }
    public string LienNumber { get; private set; } = string.Empty;

    // Actor linkages
    public Guid SellingOrganizationId { get; private set; }
    public Guid? BuyingOrganizationId { get; private set; }
    public Guid? HoldingOrganizationId { get; private set; }

    // Subject linkage
    public Guid? SubjectPartyId { get; private set; }

    // Partition key
    public Guid TenantId { get; private set; }

    // Lien details
    public string LienType { get; private set; } = string.Empty;
    public decimal OriginalAmount { get; private set; }
    public decimal? OfferPrice { get; private set; }
    public decimal? PurchasePrice { get; private set; }
    public decimal? OutstandingBalance { get; private set; }
    public string CurrencyCode { get; private set; } = "USD";

    // Case context
    public string? CaseReference { get; private set; }
    public DateOnly? IncidentDate { get; private set; }
    public DateOnly? ExpectedSettlementDate { get; private set; }
    public string? JurisdictionState { get; private set; }

    // Subject snapshot
    public string? SubjectFirstName { get; private set; }
    public string? SubjectLastName { get; private set; }
    public DateOnly? SubjectDob { get; private set; }

    // Workflow
    public string Status { get; private set; } = "Draft";
    public bool IsConfidential { get; private set; }

    // Navigation collections (private backing fields)
    private readonly List<LienStatusHistory> _statusHistories = new();
    private readonly List<LienDocument> _documents = new();
    private readonly List<LienOffer> _offers = new();
    private readonly List<LienSettlementEvent> _settlementEvents = new();

    public IReadOnlyCollection<LienStatusHistory> StatusHistories  => _statusHistories.AsReadOnly();
    public IReadOnlyCollection<LienDocument>      Documents        => _documents.AsReadOnly();
    public IReadOnlyCollection<LienOffer>         Offers           => _offers.AsReadOnly();
    public IReadOnlyCollection<LienSettlementEvent> SettlementEvents => _settlementEvents.AsReadOnly();

    // Navigation (one-to-one, nullable until purchased)
    public LienPurchaseEvent? PurchaseEvent { get; private set; }

    private Lien() { }

    public static Lien Create(
        Guid tenantId, Guid sellingOrgId, string lienNumber,
        string lienType, decimal originalAmount, string currencyCode,
        Guid? subjectPartyId, string? subjectFirstName, string? subjectLastName, DateOnly? subjectDob,
        bool isConfidential, Guid? createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lienNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(lienType);
        if (originalAmount <= 0) throw new ArgumentOutOfRangeException(nameof(originalAmount));

        var now = DateTime.UtcNow;
        return new Lien
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SellingOrganizationId = sellingOrgId,
            LienNumber = lienNumber,
            LienType = lienType,
            OriginalAmount = originalAmount,
            OutstandingBalance = originalAmount,
            CurrencyCode = currencyCode,
            SubjectPartyId = subjectPartyId,
            SubjectFirstName = subjectFirstName,
            SubjectLastName = subjectLastName,
            SubjectDob = subjectDob,
            IsConfidential = isConfidential,
            Status = "Draft",
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void TransitionStatus(string to, Guid? byUserId, Guid? byOrgId, string? reason = null)
    {
        var from = Status;
        ValidateTransition(from, to);
        Status = to;
        UpdatedByUserId = byUserId;
        _statusHistories.Add(LienStatusHistory.Create(Id, from, to, byUserId, byOrgId, reason));
    }

    private static void ValidateTransition(string from, string to)
    {
        var allowed = new Dictionary<string, string[]>
        {
            ["Draft"]       = ["Offered", "Cancelled"],
            ["Offered"]     = ["UnderReview", "Cancelled"],
            ["UnderReview"] = ["Offered", "Purchased"],
            ["Purchased"]   = ["Active", "Cancelled"],
            ["Active"]      = ["Settled", "Disputed"],
            ["Disputed"]    = ["Active", "Settled"],
        };
        if (!allowed.TryGetValue(from, out var targets) || !targets.Contains(to))
            throw new InvalidOperationException(
                $"Lien status transition '{from}' → '{to}' is not permitted.");
    }
}

// Lien.Domain/LienStatusHistory.cs
public class LienStatusHistory
{
    public Guid Id { get; private set; }
    public Guid LienId { get; private set; }
    public string FromStatus { get; private set; } = string.Empty;
    public string ToStatus { get; private set; } = string.Empty;
    public Guid? ChangedByUserId { get; private set; }
    public Guid? ChangedByOrgId { get; private set; }
    public string? Reason { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }

    public Lien Lien { get; private set; } = null!;

    public static LienStatusHistory Create(
        Guid lienId, string from, string to,
        Guid? userId, Guid? orgId, string? reason = null)
        => new()
        {
            Id = Guid.NewGuid(), LienId = lienId,
            FromStatus = from, ToStatus = to,
            ChangedByUserId = userId, ChangedByOrgId = orgId,
            Reason = reason, ChangedAtUtc = DateTime.UtcNow
        };
}

// Lien.Domain/LienOffer.cs
public class LienOffer
{
    public Guid Id { get; private set; }
    public Guid LienId { get; private set; }
    public Guid BuyerOrgId { get; private set; }
    public Guid? OfferedByUserId { get; private set; }
    public decimal? OfferedAmount { get; private set; }
    public string Status { get; private set; } = "Pending"; // Pending|Accepted|Declined|Withdrawn|Superseded
    public string? DecisionReason { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public Lien Lien { get; private set; } = null!;
}

// Lien.Domain/LienPurchaseEvent.cs
public class LienPurchaseEvent
{
    public Guid Id { get; private set; }
    public Guid LienId { get; private set; }
    public Guid? LienOfferId { get; private set; }
    public Guid BuyerOrgId { get; private set; }
    public Guid? HolderOrgId { get; private set; }
    public Guid? PurchasedByUserId { get; private set; }
    public decimal PurchasePrice { get; private set; }
    public string? PurchaseMethod { get; private set; }
    public string? PaymentConfirmRef { get; private set; }
    public DateOnly EffectiveDate { get; private set; }
    public DateTime PurchasedAtUtc { get; private set; }

    public Lien Lien { get; private set; } = null!;
    public LienOffer? LienOffer { get; private set; }
}

// Lien.Domain/LienSettlementEvent.cs
public class LienSettlementEvent
{
    public Guid Id { get; private set; }
    public Guid LienId { get; private set; }
    public Guid SettledByOrgId { get; private set; }
    public Guid? SettledByUserId { get; private set; }
    public string SettlementType { get; private set; } = string.Empty;
    public decimal AmountRecovered { get; private set; }
    public decimal BalanceBeforeEvent { get; private set; }
    public decimal BalanceAfterEvent { get; private set; }
    public bool IsFinal { get; private set; }
    public string? SettlementRef { get; private set; }
    public string? Notes { get; private set; }
    public DateOnly EffectiveDate { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }

    public Lien Lien { get; private set; } = null!;
}
```

### EF Relationships

```
Lien (1) ──< LienStatusHistories   (many)       [cascade delete]
Lien (1) ──< LienDocuments         (many)       [cascade delete]
Lien (1) ──< LienOffers            (many)       [cascade delete]
Lien (1) ──  LienPurchaseEvent     (0..1)       [cascade delete; unique index enforces 0..1]
Lien (1) ──< LienSettlementEvents  (many)       [cascade delete]
LienOffer (0..1) ──  LienPurchaseEvent (0..1)  [restrict delete]
```

---

## 6. Migration Plan

### Current State

No `Liens` service exists. This is a greenfield implementation. However, the Identity service already has the product roles and capabilities seeded (`SYNQLIEN_SELLER` / `SYNQLIEN_BUYER` / `SYNQLIEN_HOLDER`), so the authorization infrastructure is live.

### Phase A — Create `lien_db` schema

**Migration `20260329300001_InitialLienSchema`**

Creates the `Liens` table, all indexes. All columns non-null where possible; org and party FK columns nullable. Use `CREATE TABLE IF NOT EXISTS` throughout.

Adds `ConnectionStrings__LienDb` to the service configuration (environment secret, same pattern as `CareConnectDb` and `FundDb`).

### Phase B — Create supporting tables

**Migration `20260329300002_AddLienSupportingTables`**

Creates `LienStatusHistories`, `LienDocuments`, `LienOffers`, `LienPurchaseEvents`, `LienSettlementEvents` with `CREATE TABLE IF NOT EXISTS` and all foreign keys.

### Phase C — Backward compatibility shim (if migrating from a legacy system)

If existing lien data is ingested from an external system or spreadsheet:
- All ingested liens enter at `Draft` status.
- `SellingOrganizationId` is set to the importing organization.
- `BuyingOrganizationId` / `HoldingOrganizationId` are null; organizations must claim purchased liens through the platform workflow.
- A synthetic `LienStatusHistory` row is inserted for each import: `(null → Draft, reason: 'Imported from legacy system')`.

### MySQL Pattern

All `ALTER TABLE` statements must use the `INFORMATION_SCHEMA` conditional `PREPARE/EXECUTE` pattern — `ADD COLUMN IF NOT EXISTS` is not valid in MySQL 8.0 on AWS RDS:

```sql
SET @s = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Liens'
    AND COLUMN_NAME='BuyingOrganizationId')=0,
  'ALTER TABLE `Liens` ADD COLUMN `BuyingOrganizationId` char(36) NULL',
  'SELECT 1');
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;
```

---

## 7. Authorization Rules

### JWT Claims Available

| Claim | Example |
|---|---|
| `tenant_id` | `20000000-...-001` |
| `org_id` | `40000000-...-001` |
| `org_type` | `PROVIDER` / `LIEN_OWNER` |
| `product_roles` | `["SYNQLIEN_SELLER"]` |

### Provider Seller (`org_type = PROVIDER`, role = `SYNQLIEN_SELLER`)

```csharp
// Create lien
// POST /api/liens
// - org_type must be PROVIDER
// - Sets SellingOrganizationId = orgId from JWT
// - Status starts at Draft; lien is private

// List own liens
// GET /api/liens
// - WHERE SellingOrganizationId = orgId
// - All statuses visible to seller

// Offer lien
// POST /api/liens/{id}/offer
// - Validates SellingOrganizationId = orgId
// - Requires OfferPrice to be set in request
// - Requires at least one LienDocument (LienInstrument type)
// - Transitions Draft → Offered; lien becomes browseable

// Withdraw lien
// POST /api/liens/{id}/cancel
// - Only valid from Draft or Offered
// - Validates SellingOrganizationId = orgId
// - Any Pending LienOffers are marked Superseded
```

### Lien Owner Buyer (`org_type = LIEN_OWNER`, role = `SYNQLIEN_BUYER`)

```csharp
// Browse offered liens
// GET /api/liens/marketplace
// - WHERE Status = 'Offered'
// - Subject fields hidden if IsConfidential = true
// - No org filter (public marketplace within the platform)

// Submit offer
// POST /api/liens/{id}/offers
// - Status must be 'Offered' or 'UnderReview' (to allow competing offers)
// - Creates LienOffer { BuyerOrgId = orgId, OfferedAmount, ExpiresAt }
// - If first offer: transitions Offered → UnderReview

// Confirm purchase
// POST /api/liens/{id}/purchase
// - Validates active LienOffer belongs to orgId (Status = Pending)
// - Creates LienPurchaseEvent
// - Sets Lien.BuyingOrganizationId = orgId
// - Sets Lien.HoldingOrganizationId = orgId (default; may be reassigned)
// - Transitions UnderReview → Purchased
// - Other pending LienOffers on the same lien are marked Superseded

// View purchased liens
// GET /api/liens?purchased=true
// - WHERE BuyingOrganizationId = orgId
```

### Lien Holder (`org_type = LIEN_OWNER`, role = `SYNQLIEN_HOLDER`)

```csharp
// Activate (begin servicing)
// POST /api/liens/{id}/activate
// - Validates HoldingOrganizationId = orgId
// - Transitions Purchased → Active

// Record settlement payment
// POST /api/liens/{id}/settlements
// - Validates HoldingOrganizationId = orgId AND Status IN ('Active', 'Disputed')
// - Creates LienSettlementEvent
// - If IsFinal: transitions Active|Disputed → Settled

// Raise dispute
// POST /api/liens/{id}/dispute
// - Validates HoldingOrganizationId = orgId AND Status = 'Active'
// - Transitions Active → Disputed

// View held liens
// GET /api/liens?held=true
// - WHERE HoldingOrganizationId = orgId

// Reassign holder (see §9)
// POST /api/liens/{id}/reassign-holder
// - Validates BuyingOrganizationId = orgId (buyer must authorize the reassignment)
// - Sets HoldingOrganizationId = request.NewHolderOrgId
// - Writes status history entry
```

### Endpoint-Level Enforcement Example

```csharp
app.MapPost("/api/liens/{id}/purchase", async (
    Guid id,
    PurchaseLienRequest req,
    ICurrentRequestContext ctx,
    ILienService svc,
    CancellationToken ct) =>
{
    if (ctx.OrgType != OrgType.LienOwner)
        return Results.Forbid();
    if (!ctx.ProductRoles.Contains("SYNQLIEN_BUYER"))
        return Results.Forbid();

    var result = await svc.PurchaseAsync(id, ctx.OrgId, ctx.UserId, req, ct);
    return Results.Ok(result);
})
.RequireAuthorization("SynqLienBuyer");  // policy checks product_roles claim
```

---

## 8. Cross-Tenant Access Model

### The Problem

A provider on tenant `legalsynq.medclinic-alpha.com` lists a lien. A lien-owner fund on tenant `legalsynq.lienbuyer-beta.com` wants to browse and purchase it. The `Liens` row lives in `lien_db` under `TenantId = medclinic-alpha-tenant-id`. A buyer querying `WHERE TenantId = their-own-tenant-id` sees nothing.

### The Solution: Organization-Scoped Queries

```sql
-- Provider query: "my inventory, any status"
SELECT * FROM Liens
WHERE SellingOrganizationId = @orgId
ORDER BY CreatedAtUtc DESC;

-- Public marketplace: "all listed liens, any tenant's provider"
SELECT Id, LienNumber, LienType, OriginalAmount, OfferPrice, CurrencyCode,
       JurisdictionState, Status,
       -- Subject fields conditionally included based on IsConfidential:
       IF(IsConfidential = 0, SubjectFirstName, NULL) AS SubjectFirstName,
       IF(IsConfidential = 0, SubjectLastName,  NULL) AS SubjectLastName
FROM Liens
WHERE Status = 'Offered'
ORDER BY CreatedAtUtc DESC;

-- Buyer query: "liens I have purchased or am reviewing"
SELECT * FROM Liens
WHERE BuyingOrganizationId = @orgId
ORDER BY UpdatedAtUtc DESC;

-- Holder query: "liens I am currently servicing"
SELECT * FROM Liens
WHERE HoldingOrganizationId = @orgId
  AND Status IN ('Active', 'Disputed')
ORDER BY ExpectedSettlementDate ASC;
```

### TenantId Usage in Cross-Tenant Context

`TenantId` is still written to every row for compliance partitioning, storage cost attribution, and tenant-scoped admin queries by platform operators. It is **never** used as a filter in the application service layer for organization-facing operations. API routes serving buyers and holders must not accept `TenantId` as a query parameter.

### Org Discovery

Before submitting an offer or reassigning a holder, callers need to look up valid `LIEN_OWNER` org IDs. The Identity service exposes an internal directory of active organizations by `org_type`, which the Lien service calls server-side during validation. This is the only cross-service coordination step needed at purchase time.

---

## 9. Purchase and Settlement Lifecycle

### 9.1 Purchase Event Creation

```
1. Lien is in 'Offered' status.

2. Buyer submits POST /api/liens/{id}/offers
   → Creates LienOffer { Status = Pending, BuyerOrgId, OfferedAmount }
   → If first offer on this lien: Lien transitions Offered → UnderReview
   → Seller is notified (event/notification)

3. Seller may accept or decline the offer:
   PATCH /api/liens/{id}/offers/{offerId}/accept
   → LienOffer.Status = Accepted
   → Triggers step 4

4. Buyer confirms:
   POST /api/liens/{id}/purchase
   → Validates: LienOffer.Status = Accepted AND LienOffer.BuyerOrgId = caller's orgId
   → Creates LienPurchaseEvent {
         BuyerOrgId, HolderOrgId (= BuyerOrgId by default),
         PurchasePrice = LienOffer.OfferedAmount ?? Lien.OfferPrice,
         EffectiveDate = today
     }
   → Sets Lien.BuyingOrganizationId = BuyerOrgId
   → Sets Lien.HoldingOrganizationId = HolderOrgId
   → Sets Lien.PurchasePrice = PurchasePrice
   → Other Pending LienOffers for this lien marked Superseded
   → Lien transitions UnderReview → Purchased
```

### 9.2 Holder Assignment

At purchase time, `HoldingOrganizationId` defaults to `BuyingOrganizationId`. A buyer may designate a separate servicing entity in the purchase request:

```
POST /api/liens/{id}/purchase
{ "holderOrgId": "<different-lien-owner-org>" }
→ LienPurchaseEvent.HolderOrgId = holderOrgId
→ Lien.HoldingOrganizationId = holderOrgId
```

After purchase, the buyer may reassign the holder at any time while `Status IN ('Purchased', 'Active', 'Disputed')`:

```
POST /api/liens/{id}/reassign-holder
{ "newHolderOrgId": "<lien-owner-org>" }
→ Validates: caller's orgId = Lien.BuyingOrganizationId (only buyer can reassign)
→ Validates: newHolderOrgId has SYNQLIEN_HOLDER role
→ Sets Lien.HoldingOrganizationId = newHolderOrgId
→ Writes LienStatusHistory entry: reason "Holder reassigned to <orgId>"
→ Does NOT change Lien.Status (no transition needed)
```

### 9.3 Settlement Lifecycle and Constraints

```
Holder records each payment received against the lien:

POST /api/liens/{id}/settlements
{
  "settlementType": "CaseSettlement",
  "amountRecovered": 4500.00,
  "balanceBefore": 8000.00,   // validated against Lien.OutstandingBalance
  "effectiveDate": "2026-04-15",
  "settlementRef": "CASE-2024-00891",
  "isFinal": false            // partial payment
}
→ Creates LienSettlementEvent
→ Sets Lien.OutstandingBalance -= amountRecovered
→ If isFinal = false: Lien stays Active

POST /api/liens/{id}/settlements
{
  "settlementType": "PaymentInFull",
  "amountRecovered": 3500.00,
  "balanceBefore": 3500.00,
  "isFinal": true
}
→ Creates LienSettlementEvent { IsFinal = true }
→ Sets Lien.OutstandingBalance = 0
→ Lien transitions Active → Settled  [terminal]
```

Settlement constraints:
- `AmountRecovered` must be > 0
- `BalanceBefore` must equal `Lien.OutstandingBalance` at time of call (optimistic concurrency)
- `BalanceAfterEvent` = `BalanceBefore - AmountRecovered` (must be ≥ 0)
- Only the current `HoldingOrganizationId` may record settlement events
- `Settled` is a terminal status; no further events allowed

---

## 10. Risks and Edge Cases

### 10.1 Duplicate Lien Offers

**Risk:** The same lien is listed and offered twice (e.g., two separate `Offered` listings for the same underlying receivable).

**Mitigation:**
- `(TenantId, LienNumber)` is unique — exact duplicate lien numbers within a tenant are prevented at the database level.
- The service layer warns if an existing non-cancelled lien exists for the same `SubjectPartyId + SellingOrganizationId + LienType` combination (soft duplicate detection).
- Sellers must explicitly cancel an active listing before creating a replacement with corrected terms; they cannot simply re-offer without audit trail.

### 10.2 Same Lien Offered to Multiple Buyers Simultaneously

**Risk:** Multiple `LienOffer` records exist in `Pending` status simultaneously — what happens when one is accepted?

**Mitigation:**
- The platform **intentionally allows** multiple competing `Pending` LienOffers on a single lien in `UnderReview` status. This creates a mini-auction.
- When the seller accepts one offer (or the buyer confirms purchase), all other `Pending` LienOffers for the same lien are atomically set to `Superseded` in the same transaction.
- The `LienPurchaseEvents` table has a `UNIQUE INDEX` on `LienId`, enforcing that only one purchase can complete per lien at the database level as a final guard.
- Any buyer whose offer is superseded is notified and may browse other available liens.

### 10.3 Reassignment of Holder

**Risk:** The buyer reassigns the holder to a third-party servicer who is not known to the seller; the seller has no visibility.

**Mitigation:**
- Seller has no right of refusal over holder reassignment — the lien is sold; the seller's obligation ends at purchase. The `LienStatusHistories` table records every reassignment with timestamps and user IDs for full auditability.
- Platform admin can query `LienStatusHistories WHERE Reason LIKE 'Holder reassigned%'` for compliance review.
- The new holder must have the `SYNQLIEN_HOLDER` role validated at reassignment time; an inactive or unregistered org cannot be assigned.

### 10.4 Settlement After Purchase Without Activation

**Risk:** A buyer purchases a lien but never calls `POST /activate`; they attempt to record a settlement directly from `Purchased` status.

**Mitigation:**
- `TransitionStatus` rejects `Purchased → Settled` (not in the allowed matrix). Settlement events require `Status IN ('Active', 'Disputed')`.
- The service layer enforces this: `POST /api/liens/{id}/settlements` returns HTTP 409 if `Status != 'Active' AND Status != 'Disputed'` with a clear message: "Lien must be activated before recording settlement events."
- This ensures the activation acknowledgement (and its history record) is never skipped.

### 10.5 Subject Party Mismatch

**Risk:** The provider creates a lien with an inline subject name, then later links `SubjectPartyId` to a Party record whose name differs from the snapshot.

**Mitigation:**
- `SubjectPartyId` can only be set at lien creation time or updated during `Draft` status only.
- When `SubjectPartyId` is set, the service calls CareConnect's Party read endpoint and compares `SubjectFirstName`/`SubjectLastName`/`SubjectDob` against the Party record. A mismatch blocks the operation with HTTP 422, listing the discrepant fields.
- Once the lien reaches `Offered` status, `SubjectPartyId` and subject snapshot fields are locked (immutable) to prevent bait-and-switch.

### 10.6 Lien Offered After Regulatory Expiry

**Risk:** A provider lists a lien instrument that has expired under state law (e.g., a medical lien that was not perfected within the statutory period).

**Mitigation:**
- The `ExpectedSettlementDate` field is available for holder tracking, but is not a regulatory expiry guard.
- The platform does not enforce jurisdiction-specific lien law (out of scope for Phase 1).
- Providers and buyers are responsible for their own legal compliance. The `JurisdictionState` field surfaces the state so buyers can apply their own due diligence rules.
- Future Phase 2: add `LienExpiryDate` and a background job that auto-transitions `Offered` liens past their expiry to `Cancelled` with reason "Statutory expiry".

### 10.7 Provider Org Deactivated After Offering

**Risk:** A provider organization is deactivated on the platform while one or more of its liens are in `Offered` or `UnderReview` status.

**Mitigation:**
- Liens remain in their current status; they do not auto-cancel.
- A nightly platform job detects `SellingOrganizationId` pointing to an inactive org and transitions affected `Offered`/`UnderReview` liens to `Cancelled` with reason "Selling organization deactivated."
- Any pending `LienOffers` are simultaneously marked `Superseded`.
- `Active` and `Purchased` liens owned by the seller are unaffected (ownership has passed to the buyer).

---

*Document status: Design complete. Implementation sequencing: Phase A migration (schema) → Phase B migration (supporting tables) → Domain entity + EF config implementation → LienService + repository → Endpoints (create, offer, browse, purchase, settle) → Cross-service Party validation integration.*
