# LegalSynq CareConnect — Party Model & Multi-Org Workflow Refactor
## Design & Implementation Reference

**Baseline:** CareConnect service as currently implemented. Referrals today use flat inline
client fields (`ClientFirstName`, `ClientLastName`, `ClientDob`, `ClientPhone`, `ClientEmail`)
and a single `TenantId` as the sole partition key. No organization-level access control exists.

---

## 1. Party Model Design

### Why Party Is a First-Class Entity

A Party is a real-world individual (injured client, referred patient) who is the **subject** of a
workflow — not a user of the platform. Collapsing party data into inline referral fields causes:

- Duplication when the same client is referred multiple times
- No stable identity to link appointments, notes, or fund applications
- No path to a future injured-party portal (requires a stable PartyId)
- PII is replicated across every referral row

### `Parties` Table

```
Parties
├── Id                  char(36)    PK
├── TenantId            char(36)    FK → identity_db.Tenants (logical reference, no physical FK)
├── OwnerOrganizationId char(36)    FK → identity_db.Organizations (org that created the record)
├── PartyType           varchar(20) INDIVIDUAL (only value for now; future: ENTITY)
├── FirstName           varchar(100)
├── LastName            varchar(100)
├── MiddleName          varchar(100) nullable
├── PreferredName       varchar(100) nullable
├── DateOfBirth         date         nullable
├── SsnLast4            char(4)      nullable — last 4 only, never full SSN
├── LinkedUserId        char(36)     nullable — future: injured party portal login
├── IsActive            tinyint(1)
├── CreatedByUserId     char(36)     nullable
├── CreatedAtUtc        datetime(6)
└── UpdatedAtUtc        datetime(6)
```

**Indexes:**
- `(TenantId, LastName, FirstName)` — name search within tenant
- `(OwnerOrganizationId)` — org-scoped listing
- `(TenantId, LinkedUserId)` — future portal user linkage
- `(TenantId, DateOfBirth, LastName)` — duplicate detection

### `PartyContacts` Table

Separate contact records allow multiple phones/emails and support future contact type expansion.

```
PartyContacts
├── Id          char(36)    PK
├── PartyId     char(36)    FK → Parties CASCADE
├── ContactType varchar(20) PHONE, EMAIL, ALTERNATE_PHONE
├── Value       varchar(320)
├── IsPrimary   tinyint(1)
├── IsVerified  tinyint(1)
└── CreatedAtUtc datetime(6)
```

**Unique index:** `(PartyId, ContactType, Value)` — no duplicate entries per party.
**Index:** `(ContactType, Value)` — lookup by phone or email across parties.

### `PartyAddresses` Table (future phase, defined now)

```
PartyAddresses
├── Id           char(36)    PK
├── PartyId      char(36)    FK → Parties CASCADE
├── AddressType  varchar(20) HOME, MAILING, WORK
├── Line1        varchar(200)
├── Line2        varchar(200) nullable
├── City         varchar(100)
├── State        varchar(50)
├── PostalCode   varchar(20)
├── Country      char(2)      default 'US'
├── IsPrimary    tinyint(1)
└── CreatedAtUtc datetime(6)
```

### Future Link: Party → User (Injured Party Portal)

`Parties.LinkedUserId` is a nullable foreign key to `identity_db.Users`. It is left null until
the injured party registers for portal access. The link is created by the Identity service when
a portal invitation is accepted. Product services never write this column directly.

**Token for portal access:** A short-lived `PartySession` token (15 minutes, single-purpose)
is issued by the Identity service on invitation link click. It carries a `party_id` claim
instead of `org_id`/`product_roles`. CareConnect validates the `party_id` claim for read-only
status views rather than the standard capability chain.

### When to Store Snapshot Fields on Workflow Records

Even after linking `SubjectPartyId`, the referral record keeps **snapshot fields** for the moment
of submission. This protects data integrity when party records change later:

| Field | On Party record | On Referral snapshot |
|---|---|---|
| Full name | Live (editable) | `SubjectNameSnapshot` — frozen at creation |
| Date of birth | Live | `SubjectDobSnapshot` — frozen at creation |
| Phone | Live (editable) | Omit — get from PartyContacts on demand |
| Email | Live (editable) | Omit — get from PartyContacts on demand |

Rationale: a party's name may be corrected, but the referral was submitted under the name as
known at that time. Courts and insurers care about the submission-time name.

---

## 2. CareConnect Referral Schema Refactor

### Current Schema (Existing)

```
Referrals
  TenantId              ← sole partition key
  ProviderId            ← FK to Providers
  ClientFirstName       ← inline, will move to Party
  ClientLastName        ← inline, will move to Party
  ClientDob             ← inline, will move to Party
  ClientPhone           ← inline, will move to Party
  ClientEmail           ← inline, will move to Party
  CaseNumber
  RequestedService
  Urgency
  Status
  Notes
```

### Target Schema (After Refactor)

```
Referrals
├── Id                        char(36)     PK
├── TenantId                  char(36)     Still present — fast partition key for query scoping
├── ReferringOrganizationId   char(36)     FK → Organizations (LAW_FIRM sending org)
├── ReceivingOrganizationId   char(36)     FK → Organizations (PROVIDER receiving org)
├── SubjectPartyId            char(36)     FK → Parties (the client)
│
├── SubjectNameSnapshot       varchar(200) FirstName + LastName at submission time
├── SubjectDobSnapshot        date         DOB at submission time (nullable)
│
├── CaseNumber                varchar(100) nullable
├── RequestedService          varchar(200)
├── Urgency                   varchar(20)  Low / Normal / Urgent / Emergency
├── Status                    varchar(20)  New / Received / Contacted / Scheduled / Completed / Cancelled
├── Notes                     text         nullable — initial referral note (shared by default)
│
├── CreatedByUserId           char(36)     nullable
├── UpdatedByUserId           char(36)     nullable
├── CreatedAtUtc              datetime(6)
└── UpdatedAtUtc              datetime(6)
```

**Columns dropped from current schema** (after backfill is complete and verified):
- `ClientFirstName`, `ClientLastName`, `ClientDob`, `ClientPhone`, `ClientEmail` — all now live
  in `Parties` / `PartyContacts`. Snapshot columns replace the name/DOB fields.

**Columns added:**
- `ReferringOrganizationId` — identifies which law firm org created the referral
- `ReceivingOrganizationId` — identifies which provider org is receiving it
- `SubjectPartyId` — links to the Party record
- `SubjectNameSnapshot` — immutable name at submission time
- `SubjectDobSnapshot` — immutable DOB at submission time

### Indexes

| Index name | Columns | Purpose |
|---|---|---|
| `IX_Referrals_TenantId_Status` | `(TenantId, Status)` | Tenant-scoped status filter |
| `IX_Referrals_ReferringOrgId_Status` | `(ReferringOrganizationId, Status)` | Law firm "sent" list |
| `IX_Referrals_ReceivingOrgId_Status` | `(ReceivingOrganizationId, Status)` | Provider "inbox" |
| `IX_Referrals_SubjectPartyId` | `SubjectPartyId` | All referrals for a party |
| `IX_Referrals_CreatedAtUtc` | `CreatedAtUtc` | Date range filtering |

---

## 3. Appointment Schema Update

### Current Schema

```
Appointments
  TenantId, ReferralId, ProviderId, FacilityId, ServiceOfferingId,
  AppointmentSlotId, ScheduledStartAtUtc, ScheduledEndAtUtc, Status, Notes
```

### Target Additions

```
Appointments (additions only)
├── ReferringOrganizationId   char(36)  nullable FK — carried from Referral at create time
├── ReceivingOrganizationId   char(36)  nullable FK — the provider org scheduling the appointment
└── SubjectPartyId            char(36)  nullable FK → Parties
```

These three columns are **denormalized copies** from the linked Referral, stamped at appointment
creation time. They enable:
- A provider to query their appointments without joining to Referrals
- Cross-org appointment display without revealing unrelated referral fields
- Filtering appointments by party for a case summary

**Relationship to Referrals:**
Every appointment belongs to exactly one referral (`Appointments.ReferralId → Referrals.Id`).
The org and party fields on the appointment duplicate those on the referral intentionally —
this avoids a join on every appointment list query and preserves the snapshot at scheduling time
even if the referral's org fields are later corrected.

### Indexes Added

| Index | Columns | Purpose |
|---|---|---|
| `IX_Appointments_ReceivingOrgId_Status` | `(ReceivingOrganizationId, Status)` | Provider appointment inbox |
| `IX_Appointments_SubjectPartyId` | `SubjectPartyId` | All appointments for a party |

---

## 4. Notes and Attachments Visibility Model

### Current Problem

`ReferralNote.IsInternal` is a boolean. It does not specify *which* org's internal notes are
hidden from *which* other org. When a referral crosses tenant boundaries:
- The law firm's internal notes must never be visible to the provider
- The provider's internal notes must never be visible to the law firm
- Some notes are intentionally shared across the org boundary

### Proposed `VisibilityScope` Model

Replace `IsInternal bool` with:
- `OwnerOrganizationId char(36)` — which org created this note
- `VisibilityScope varchar(20)` — `INTERNAL` or `SHARED`

```
ReferralNotes (target)
├── Id                    char(36)
├── TenantId              char(36)    Referencing org's tenant
├── ReferralId            char(36)    FK → Referrals
├── OwnerOrganizationId   char(36)    Org that created the note
├── NoteType              varchar(50)
├── Content               text
├── VisibilityScope       varchar(20) INTERNAL | SHARED
├── CreatedByUserId       char(36)    nullable
├── CreatedAtUtc          datetime(6)
└── UpdatedAtUtc          datetime(6)
```

### Visibility Rules

| Note's `VisibilityScope` | Note's `OwnerOrganizationId` | Visible to Referring org | Visible to Receiving org |
|---|---|---|---|
| `INTERNAL` | Referring org | ✅ Yes | ❌ No |
| `INTERNAL` | Receiving org | ❌ No | ✅ Yes |
| `SHARED` | Either org | ✅ Yes | ✅ Yes |

**Query filter applied at service layer:**
```csharp
// For a referrer viewing notes on a referral they own:
notes.Where(n => n.ReferralId == referralId
             && (n.VisibilityScope == "SHARED"
                 || n.OwnerOrganizationId == user.OrgId))

// For a receiver viewing notes on a referral addressed to them:
notes.Where(n => n.ReferralId == referralId
             && (n.VisibilityScope == "SHARED"
                 || n.OwnerOrganizationId == user.OrgId))
```

The symmetry is intentional — both sides apply the same filter logic. Each org sees SHARED notes
plus their own INTERNAL notes. They never see the other org's INTERNAL notes.

### Attachments — Same Model

`ReferralAttachment` gets the same two columns: `OwnerOrganizationId` and `VisibilityScope`.
The same filter rules apply.

### `AppointmentNote` Visibility

Appointment notes use the same `OwnerOrganizationId` + `VisibilityScope` model. The receiving
provider's internal appointment notes (e.g., clinical observations) should never flow back to
the referring law firm.

---

## 5. EF Core Implementation Plan

### New Entity: `Party`

```csharp
// CareConnect.Domain/Party.cs
public class Party
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OwnerOrganizationId { get; private set; }
    public string PartyType { get; private set; } = "INDIVIDUAL";
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? MiddleName { get; private set; }
    public string? PreferredName { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public string? SsnLast4 { get; private set; }
    public Guid? LinkedUserId { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public ICollection<PartyContact> Contacts { get; private set; } = [];

    private Party() { }

    public static Party Create(
        Guid tenantId,
        Guid ownerOrganizationId,
        string firstName,
        string lastName,
        string? middleName,
        DateOnly? dateOfBirth,
        Guid? createdByUserId)
    {
        var now = DateTime.UtcNow;
        return new Party
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OwnerOrganizationId = ownerOrganizationId,
            PartyType = "INDIVIDUAL",
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            MiddleName = middleName?.Trim(),
            DateOfBirth = dateOfBirth,
            IsActive = true,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }
}
```

### New Entity: `PartyContact`

```csharp
// CareConnect.Domain/PartyContact.cs
public class PartyContact
{
    public Guid Id { get; private set; }
    public Guid PartyId { get; private set; }
    public string ContactType { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }
    public bool IsVerified { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Party? Party { get; private set; }

    private PartyContact() { }

    public static PartyContact Create(
        Guid partyId, string contactType, string value, bool isPrimary)
        => new()
        {
            Id = Guid.NewGuid(),
            PartyId = partyId,
            ContactType = contactType.ToUpperInvariant(),
            Value = value.Trim(),
            IsPrimary = isPrimary,
            IsVerified = false,
            CreatedAtUtc = DateTime.UtcNow
        };
}
```

### Updated Entity: `Referral`

Additions to the existing `Referral` entity class:

```csharp
// Additional properties (add to existing Referral.cs)
public Guid ReferringOrganizationId { get; private set; }
public Guid ReceivingOrganizationId { get; private set; }
public Guid? SubjectPartyId { get; private set; }
public string SubjectNameSnapshot { get; private set; } = string.Empty;
public DateOnly? SubjectDobSnapshot { get; private set; }

public Party? SubjectParty { get; private set; }
```

Updated `Create` factory:

```csharp
public static Referral Create(
    Guid tenantId,
    Guid referringOrganizationId,
    Guid receivingOrganizationId,
    Guid providerId,
    Guid? subjectPartyId,
    string subjectNameSnapshot,
    DateOnly? subjectDobSnapshot,
    // ... existing params
    Guid? createdByUserId)
```

### Updated Entity: `ReferralNote`

Replace `IsInternal bool` with:

```csharp
public Guid OwnerOrganizationId { get; private set; }
public string VisibilityScope { get; private set; } = VisibilityScopeValues.Shared;
```

Remove `IsInternal`. Add:

```csharp
public static class VisibilityScopeValues
{
    public const string Internal = "INTERNAL";
    public const string Shared   = "SHARED";
}
```

### `PartyConfiguration`

```csharp
public class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable("Parties");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.OwnerOrganizationId).IsRequired();
        builder.Property(e => e.PartyType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.MiddleName).HasMaxLength(100);
        builder.Property(e => e.PreferredName).HasMaxLength(100);
        builder.Property(e => e.DateOfBirth).HasColumnType("date");
        builder.Property(e => e.SsnLast4).HasMaxLength(4);
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasMany(e => e.Contacts)
            .WithOne(c => c.Party)
            .HasForeignKey(c => c.PartyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(new[] { "TenantId", "LastName", "FirstName" })
            .HasDatabaseName("IX_Parties_TenantId_Name");
        builder.HasIndex(e => e.OwnerOrganizationId)
            .HasDatabaseName("IX_Parties_OwnerOrganizationId");
        builder.HasIndex(new[] { "TenantId", "LinkedUserId" })
            .HasDatabaseName("IX_Parties_TenantId_LinkedUserId");
        builder.HasIndex(new[] { "TenantId", "DateOfBirth", "LastName" })
            .HasDatabaseName("IX_Parties_TenantId_Dob_Name");
    }
}
```

### DbContext Updates

```csharp
// Add to CareConnectDbContext
public DbSet<Party>        Parties        => Set<Party>();
public DbSet<PartyContact> PartyContacts  => Set<PartyContact>();
```

---

## 6. Migration Plan

All migrations are written manually (EF tooling may hang against AWS RDS).
Use `migrationBuilder.Sql(@"INSERT IGNORE ... / UPDATE ...")`. All changes are additive first;
destructive column drops happen only after backfill is confirmed.

### Phase A — Additive Structural Migrations

| Migration | Content |
|---|---|
| `20260329100001_AddParties` | Create `Parties`, `PartyContacts` tables with all indexes |
| `20260329100002_AddReferralOrgAndPartyColumns` | Add `ReferringOrganizationId`, `ReceivingOrganizationId`, `SubjectPartyId`, `SubjectNameSnapshot`, `SubjectDobSnapshot` as nullable columns to `Referrals` |
| `20260329100003_AddOrgAndPartyToAppointments` | Add `ReferringOrganizationId`, `ReceivingOrganizationId`, `SubjectPartyId` as nullable to `Appointments` |
| `20260329100004_AddVisibilityScopeToNotes` | Add `OwnerOrganizationId varchar(36)` and `VisibilityScope varchar(20)` to `ReferralNotes` and `AppointmentNotes` |

### Phase B — Backfill Data Migrations

| Migration | Content |
|---|---|
| `20260329200001_BackfillPartiesFromReferrals` | Create Party records from distinct inline client fields; link `Referrals.SubjectPartyId` |
| `20260329200002_BackfillReferralOrgIds` | Populate `ReferringOrganizationId` and `ReceivingOrganizationId` from provider → tenant linkage |
| `20260329200003_BackfillNoteVisibility` | Set `OwnerOrganizationId` = referral's referring org, `VisibilityScope = 'SHARED'` for all existing notes |

### Phase C — Deprecation Migrations (Future — after API clients migrated)

| Migration | Content | Timing |
|---|---|---|
| `20260400000001_DropReferralInlineClientFields` | Drop `ClientFirstName`, `ClientLastName`, `ClientDob`, `ClientPhone`, `ClientEmail` | After API v2 is fully adopted |
| `20260400000002_DropReferralNoteIsInternal` | Drop `IsInternal` column from `ReferralNotes` | After all read paths use `VisibilityScope` |

---

## 7. Backfill Strategy

### Step 1 — Create Party Records from Existing Referral Client Fields

```sql
-- Insert one Party per distinct (TenantId, ClientFirstName, ClientLastName, ClientDob)
-- combination. Uses a deterministic UUID via MD5 of key fields to be idempotent.
-- 'OwnerOrganizationId' is set to a known INTERNAL org as a placeholder;
-- the proper referring org is populated in Step 2 after ReferringOrganizationId is backfilled.

INSERT IGNORE INTO `Parties`
    (`Id`, `TenantId`, `OwnerOrganizationId`, `PartyType`,
     `FirstName`, `LastName`, `DateOfBirth`, `IsActive`,
     `CreatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
SELECT
    UUID(),
    r.`TenantId`,
    '40000000-0000-0000-0000-000000000001', -- placeholder: LegalSynq INTERNAL org
    'INDIVIDUAL',
    r.`ClientFirstName`,
    r.`ClientLastName`,
    r.`ClientDob`,
    1,
    NULL,
    NOW(),
    NOW()
FROM (
    SELECT DISTINCT
        `TenantId`,
        `ClientFirstName`,
        `ClientLastName`,
        `ClientDob`
    FROM `Referrals`
    WHERE `ClientFirstName` IS NOT NULL
      AND `ClientLastName`  IS NOT NULL
) AS r;
```

### Step 2 — Create PartyContacts from Referral Phone/Email

```sql
-- Phone contacts
INSERT IGNORE INTO `PartyContacts`
    (`Id`, `PartyId`, `ContactType`, `Value`, `IsPrimary`, `IsVerified`, `CreatedAtUtc`)
SELECT
    UUID(),
    p.`Id`,
    'PHONE',
    r.`ClientPhone`,
    1,
    0,
    NOW()
FROM `Referrals` r
INNER JOIN `Parties` p ON p.`TenantId`   = r.`TenantId`
                       AND p.`FirstName`  = r.`ClientFirstName`
                       AND p.`LastName`   = r.`ClientLastName`
WHERE r.`ClientPhone` IS NOT NULL AND r.`ClientPhone` != '';

-- Email contacts (similar pattern, ContactType = 'EMAIL')
```

### Step 3 — Link Referrals to SubjectPartyId

```sql
UPDATE `Referrals` r
INNER JOIN `Parties` p ON p.`TenantId`  = r.`TenantId`
                       AND p.`FirstName` = r.`ClientFirstName`
                       AND p.`LastName`  = r.`ClientLastName`
                       AND (p.`DateOfBirth` = r.`ClientDob`
                            OR (p.`DateOfBirth` IS NULL AND r.`ClientDob` IS NULL))
SET r.`SubjectPartyId`        = p.`Id`,
    r.`SubjectNameSnapshot`   = CONCAT(r.`ClientFirstName`, ' ', r.`ClientLastName`),
    r.`SubjectDobSnapshot`    = r.`ClientDob`
WHERE r.`SubjectPartyId` IS NULL;
```

### Step 4 — Propagate Org + Party IDs to Appointments

```sql
UPDATE `Appointments` a
INNER JOIN `Referrals` r ON r.`Id` = a.`ReferralId`
SET a.`ReferringOrganizationId`  = r.`ReferringOrganizationId`,
    a.`ReceivingOrganizationId`  = r.`ReceivingOrganizationId`,
    a.`SubjectPartyId`           = r.`SubjectPartyId`
WHERE a.`SubjectPartyId` IS NULL;
```

### Handling Incomplete Data Safely

- All new columns are nullable during backfill. Queries check for null during the transition window.
- Referrals where `ClientFirstName IS NULL` or blank are skipped by the Party INSERT. They retain
  `SubjectPartyId = NULL` and continue serving from inline fields via the compatibility layer.
- A post-backfill audit query surfaces unlinked referrals for manual data repair:

```sql
SELECT Id, TenantId, ClientFirstName, ClientLastName, Status, CreatedAtUtc
FROM Referrals
WHERE SubjectPartyId IS NULL
ORDER BY CreatedAtUtc DESC;
```

---

## 8. API Changes

### Referral Create — `CreateReferralRequest`

**New DTO (v2 — preferred):**

```csharp
public class CreateReferralRequest
{
    // Existing fields (still accepted during transition)
    public Guid ProviderId { get; set; }

    // NEW — org-aware routing
    public Guid? ReceivingOrganizationId { get; set; }  // if null, derived from ProviderId

    // NEW — link to existing party (preferred)
    public Guid? SubjectPartyId { get; set; }

    // Inline fields — used when SubjectPartyId is null (backward compat)
    public string? ClientFirstName { get; set; }
    public string? ClientLastName { get; set; }
    public DateTime? ClientDob { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientEmail { get; set; }

    // Unchanged
    public string? CaseNumber { get; set; }
    public string RequestedService { get; set; } = string.Empty;
    public string Urgency { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
```

**Service-layer resolution logic:**
```csharp
// In ReferralService.CreateAsync:

// 1. Resolve referring org from JWT
var referringOrgId = ctx.OrgId
    ?? throw new ValidationException("Organization context required to create a referral.");

// 2. Resolve receiving org
Guid receivingOrgId;
if (request.ReceivingOrganizationId.HasValue)
{
    receivingOrgId = request.ReceivingOrganizationId.Value;
}
else
{
    // Derive from ProviderId (backward compat)
    var provider = await _providerRepo.GetByIdAsync(request.ProviderId, ct)
        ?? throw new NotFoundException("Provider not found.");
    receivingOrgId = provider.OrganizationId;
}

// 3. Resolve or create Party
Guid subjectPartyId;
string nameSnapshot;
DateOnly? dobSnapshot;

if (request.SubjectPartyId.HasValue)
{
    var party = await _partyRepo.GetByIdAsync(request.SubjectPartyId.Value, ct)
        ?? throw new NotFoundException("Party not found.");
    subjectPartyId = party.Id;
    nameSnapshot = $"{party.FirstName} {party.LastName}";
    dobSnapshot = party.DateOfBirth;
}
else
{
    // Auto-create party from inline fields (backward compat path)
    var party = Party.Create(
        tenantId, referringOrgId,
        request.ClientFirstName ?? "Unknown",
        request.ClientLastName ?? "Unknown",
        null,
        request.ClientDob.HasValue ? DateOnly.FromDateTime(request.ClientDob.Value) : null,
        ctx.UserId);
    await _partyRepo.AddAsync(party, ct);
    subjectPartyId = party.Id;
    nameSnapshot = $"{party.FirstName} {party.LastName}";
    dobSnapshot = party.DateOfBirth;
}
```

### Referral Response — `ReferralResponse`

**Additional fields (backward compatible — old fields still present):**

```csharp
public class ReferralResponse
{
    // Existing
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RequestedService { get; set; } = string.Empty;
    public string Urgency { get; set; } = string.Empty;
    public string? CaseNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    // Legacy inline fields (still populated from snapshot or Party during transition)
    public string ClientFirstName { get; set; } = string.Empty;
    public string ClientLastName { get; set; } = string.Empty;
    public DateTime? ClientDob { get; set; }
    public string ClientPhone { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;

    // NEW
    public Guid? ReferringOrganizationId { get; set; }
    public string? ReferringOrganizationName { get; set; }
    public Guid? ReceivingOrganizationId { get; set; }
    public string? ReceivingOrganizationName { get; set; }
    public Guid? SubjectPartyId { get; set; }
    public PartyBriefResponse? SubjectParty { get; set; }
}

public class PartyBriefResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? PrimaryPhone { get; set; }
    public string? PrimaryEmail { get; set; }
}
```

### New Endpoints — Party Management

```
POST   /api/parties                  Create a party (requires party:create capability)
GET    /api/parties/{id}             Get party detail (requires party:read:own)
GET    /api/parties?name=&dob=       Search parties by name/DOB (requires party:read:own)
GET    /api/parties/{id}/referrals   All referrals for a party
```

---

## 9. Authorization Rules

### Rule Table

| Action | Allowed | Denied | Capability required |
|---|---|---|---|
| Create referral | CARECONNECT_REFERRER (LAW_FIRM) | All others | `referral:create` |
| List sent referrals | CARECONNECT_REFERRER whose org = ReferringOrganizationId | Other referrers | `referral:read:own` |
| List received referrals | CARECONNECT_RECEIVER whose org = ReceivingOrganizationId | Other receivers | `referral:read:addressed` |
| Get single referral | Referring OR receiving org | Any other org | `referral:read:own` OR `referral:read:addressed` |
| Accept referral | CARECONNECT_RECEIVER whose org = ReceivingOrganizationId only | Referring org | `referral:accept` |
| Cancel referral | CARECONNECT_REFERRER whose org = ReferringOrganizationId only | Receiving org | `referral:cancel` |
| Create party | CARECONNECT_REFERRER | Others | `party:create` |
| Read party | Org that owns the party record | Others | `party:read:own` |
| Add SHARED note | Either referring or receiving org | Others | `referral:create` (reuse) |
| Add INTERNAL note | Either org (scoped to themselves) | Not visible to other org | `referral:create` |

### Authorization Pattern — Referral Endpoints

```csharp
// GET /api/referrals — list with org-based filter
group.MapGet("/", async (
    [AsParameters] ReferralSearchParams p,
    IReferralService svc,
    ICurrentRequestContext ctx,
    ICapabilityService caps,
    CancellationToken ct) =>
{
    // Must have at least one referral capability
    var canRefer   = await caps.HasCapabilityAsync(ctx.ProductRoles, CapabilityCodes.ReferralReadOwn, ct);
    var canReceive = await caps.HasCapabilityAsync(ctx.ProductRoles, CapabilityCodes.ReferralReadAddressed, ct);

    if (!ctx.IsPlatformAdmin && !canRefer && !canReceive)
        return Results.Forbid();

    var orgId = ctx.OrgId; // null for PlatformAdmin — they see everything

    var result = await svc.SearchAsync(
        tenantId: ctx.TenantId!.Value,
        orgId: orgId,
        viewpoint: canRefer ? "REFERRER" : "RECEIVER",
        query: p.ToQuery(),
        ct);

    return Results.Ok(result);
})
.RequireAuthorization();
```

```csharp
// GET /api/referrals/{id} — single referral with cross-org boundary check
group.MapGet("/{id:guid}", async (
    Guid id,
    IReferralService svc,
    ICurrentRequestContext ctx,
    ICapabilityService caps,
    CancellationToken ct) =>
{
    var referral = await svc.GetByIdAsync(id, ct)
        ?? throw new NotFoundException($"Referral {id} not found.");

    // PlatformAdmin: bypass
    if (!ctx.IsPlatformAdmin)
    {
        var orgId = ctx.OrgId ?? throw new ForbiddenException();

        // Org must be either the referring or receiving participant
        bool isParticipant = referral.ReferringOrganizationId == orgId
                          || referral.ReceivingOrganizationId == orgId;
        if (!isParticipant)
            return Results.NotFound(); // 404 not 403 — don't reveal referral exists
    }

    return Results.Ok(svc.MapToResponse(referral, ctx.OrgId));
})
.RequireAuthorization();
```

```csharp
// POST /api/referrals — create (law firm only)
group.MapPost("/", async (
    [FromBody] CreateReferralRequest request,
    IReferralService svc,
    ICurrentRequestContext ctx,
    ICapabilityService caps,
    CancellationToken ct) =>
{
    if (!ctx.IsPlatformAdmin)
    {
        if (!await caps.HasCapabilityAsync(ctx.ProductRoles, CapabilityCodes.ReferralCreate, ct))
            return Results.Forbid();

        // Defense-in-depth: org type must be LAW_FIRM
        if (ctx.OrgType != OrgType.LawFirm)
            return Results.Forbid();
    }

    var result = await svc.CreateAsync(ctx, request, ct);
    return Results.Created($"/api/referrals/{result.Id}", result);
})
.RequireAuthorization();
```

```csharp
// POST /api/referrals/{id}/accept — provider accepts
group.MapPost("/{id:guid}/accept", async (
    Guid id,
    IReferralService svc,
    ICurrentRequestContext ctx,
    ICapabilityService caps,
    CancellationToken ct) =>
{
    if (!ctx.IsPlatformAdmin)
    {
        if (!await caps.HasCapabilityAsync(ctx.ProductRoles, CapabilityCodes.ReferralAccept, ct))
            return Results.Forbid();
    }

    var referral = await svc.GetByIdAsync(id, ct)
        ?? return Results.NotFound();

    // Receiving org must match — never reveal the referral exists to outsiders
    if (!ctx.IsPlatformAdmin && referral.ReceivingOrganizationId != ctx.OrgId)
        return Results.NotFound();

    await svc.AcceptAsync(id, ctx.UserId!.Value, ct);
    return Results.Ok();
})
.RequireAuthorization();
```

### Authorization Pattern — Notes Endpoint

```csharp
// POST /api/referrals/{id}/notes — add note
group.MapPost("/{id:guid}/notes", async (
    Guid id,
    [FromBody] CreateNoteRequest request,
    IReferralNoteService svc,
    ICurrentRequestContext ctx,
    CancellationToken ct) =>
{
    var referral = await _referralRepo.GetByIdAsync(id, ct)
        ?? return Results.NotFound();

    // Only referring or receiving org may add notes
    if (!ctx.IsPlatformAdmin)
    {
        var orgId = ctx.OrgId ?? throw new ForbiddenException();
        bool isParticipant = referral.ReferringOrganizationId == orgId
                          || referral.ReceivingOrganizationId == orgId;
        if (!isParticipant) return Results.NotFound();
    }

    // Force INTERNAL if the request says INTERNAL and org is a participant — allowed
    // Force SHARED only allowed for both orgs
    var visibilityScope = request.VisibilityScope?.ToUpperInvariant() == "INTERNAL"
        ? "INTERNAL" : "SHARED";

    var note = await svc.CreateAsync(id, ctx.OrgId!.Value, visibilityScope, request, ctx.UserId, ct);
    return Results.Created($"/api/referrals/{id}/notes/{note.Id}", note);
})
.RequireAuthorization();

// GET /api/referrals/{id}/notes — filtered by org
group.MapGet("/{id:guid}/notes", async (
    Guid id,
    IReferralNoteService svc,
    ICurrentRequestContext ctx,
    CancellationToken ct) =>
{
    // Service layer applies OwnerOrganizationId + VisibilityScope filter
    var notes = await svc.GetForReferralAsync(id, ctx.OrgId, ctx.IsPlatformAdmin, ct);
    return Results.Ok(notes);
})
.RequireAuthorization();
```

---

## 10. Risks and Edge Cases

### 1. Cross-Tenant Referral Visibility

**Risk:** A provider in Tenant B receives a referral from a law firm in Tenant A. The provider's
JWT carries `tenant_id = B`. The referral record has `TenantId = A` (the referring org's tenant).
A naive `WHERE TenantId = @tenantId` query will return 0 rows for the provider.

**Resolution:**
- Do NOT use `TenantId` as the primary filter for received referrals.
- Use `ReceivingOrganizationId = @orgId` as the primary filter for provider inbox queries.
- `TenantId` remains on the record for referring-side scoping only.
- Phase 2 will add explicit cross-tenant workflow read grants when needed.

**Interim approach (Phase 1):**
All cross-org referrals remain within the same tenant until the cross-tenant workflow grant
mechanism is built. The `ReceivingOrganizationId` field is populated now to prepare for it.

### 2. Missing or Blank Party Data on Existing Referrals

**Risk:** Some referrals were submitted with empty `ClientFirstName` or `ClientLastName` (e.g.,
data entry errors). The backfill `INSERT IGNORE` skips these rows.

**Resolution:**
- Backfill skips them intentionally (`WHERE ClientFirstName IS NOT NULL AND ClientFirstName != ''`).
- These referrals retain `SubjectPartyId = NULL` and continue reading inline fields.
- A report surfaces all unlinked referrals after backfill.
- Admin users can manually link them to existing parties via a future admin endpoint.

### 3. Duplicate Party Creation

**Risk:** Two law firm users submit referrals for the same client with slightly different name
spellings. Two Party records are created.

**Resolution short-term:**
- The create-party endpoint performs a fuzzy deduplicate check before insert:
  `WHERE TenantId = @t AND OwnerOrganizationId = @orgId AND DateOfBirth = @dob AND SOUNDEX(LastName) = SOUNDEX(@lastName)`
- If a match is found, present the existing Party to the user for confirmation before creating a new one.

**Resolution long-term:**
- A Party merge endpoint allows admin to consolidate duplicate records.
- The merge updates all `SubjectPartyId` foreign keys to point to the surviving record.

### 4. Partial Migration State

**Risk:** During the transition window, some referrals have `SubjectPartyId` populated and some
do not. API responses must work for both.

**Resolution:**
- `ReferralResponse` populates `ClientFirstName`/`ClientLastName` from:
  1. `SubjectParty.FirstName` / `SubjectParty.LastName` if `SubjectPartyId` is set.
  2. The inline `ClientFirstName`/`ClientLastName` columns if not yet migrated.
- The `SubjectParty` embedded object is `null` when not yet linked.
- Old API clients see the same flat fields; new clients use `SubjectParty`.

### 5. `ReceivingOrganizationId` Unknown for Existing Referrals

**Risk:** Existing referrals link to `ProviderId` but not to `ReceivingOrganizationId`. Providers
do not yet have an `OrganizationId` column on the `Providers` table.

**Resolution — Phase 1:**
The `Providers` table needs an `OrganizationId` column added. This links each provider profile
to the Organization record that owns it. The backfill migration then derives
`Referrals.ReceivingOrganizationId` from `Referrals.ProviderId → Providers.OrganizationId`.

Until `Providers.OrganizationId` is populated, `ReceivingOrganizationId` remains null on
historical referrals. The authorization check gracefully falls back to tenant-based scoping for
these rows.

### 6. Notes Visibility During Migration (`IsInternal → VisibilityScope`)

**Risk:** `ReferralNotes` created before the migration have `IsInternal = true/false` but no
`OwnerOrganizationId`.

**Resolution:**
- Phase C backfill sets `OwnerOrganizationId = ReferringOrganizationId` for all existing notes
  where it is null (because all existing notes were created by the referring side).
- `VisibilityScope = 'SHARED'` is the safe default for all existing notes. This preserves the
  current behavior (both sides could read them) until a future review decides if any should be
  marked INTERNAL retroactively.
- Reading code checks `VisibilityScope` first; falls back to `IsInternal` for rows where
  `OwnerOrganizationId IS NULL` (pre-migration rows).

### 7. `OrgType` Defense-in-Depth vs. Single Point of Failure

**Risk:** If capability resolution is bypassed (e.g., bug in the PlatformAdmin check), an
attacker who crafts a JWT with `product_roles = CARECONNECT_REFERRER` could create referrals.

**Resolution:**
- The JWT signature (HMAC-SHA256 with a server-held key) prevents claim injection.
- Defense-in-depth `org_type` check is applied at the service layer as a secondary guard
  (`if (ctx.OrgType != OrgType.LawFirm) return Forbid()`).
- The gateway validates the JWT signature on every request before proxying.
- Even if capability check is bypassed, the org-type guard provides a second barrier.
