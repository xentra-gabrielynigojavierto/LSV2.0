# SUP-TNT-02-01 Report — Migration Default Correction

_Status: COMPLETE_

---

## 1. Codebase Analysis

### Migration file: `20260425022101_AddTicketOwnershipFields.cs`
Both new string columns were generated with `defaultValue: ""`:
```csharp
migrationBuilder.AddColumn<string>(
    name: "requester_type", ...
    defaultValue: "")   // WRONG — should be "InternalUser"

migrationBuilder.AddColumn<string>(
    name: "visibility_scope", ...
    defaultValue: "")   // WRONG — should be "Internal"
```

### SupportDbContext entity config
```csharp
ticket.Property(t => t.RequesterType)
    .HasColumnName("requester_type").HasConversion<string>().HasMaxLength(20).IsRequired();
// Missing: .HasDefaultValue("InternalUser")

ticket.Property(t => t.VisibilityScope)
    .HasColumnName("visibility_scope").HasConversion<string>().HasMaxLength(20).IsRequired();
// Missing: .HasDefaultValue("Internal")
```

### SupportTicket C# property defaults
```csharp
public TicketRequesterType RequesterType { get; set; } = TicketRequesterType.InternalUser;
public TicketVisibilityScope VisibilityScope { get; set; } = TicketVisibilityScope.Internal;
```
C# defaults are correct. The mismatch is at the DB/migration level only.

### SupportDbContextModelSnapshot
Properties for `RequesterType` and `VisibilityScope` were present but lacked `.HasDefaultValue(...)` calls — mirroring the DbContext omission.

---

## 2. Defect Identified

**Root cause:** EF Core generates `defaultValue: ""` for `AddColumn<string>` on `NOT NULL` columns when no explicit `HasDefaultValue` is configured in the model. Because we used `HasConversion<string>()` without a `HasDefaultValue`, EF fell back to the C# `default(string)` which is null — but since `IsRequired()` was set, EF substituted empty string as a safe non-null default.

**Impact:** Any existing rows in the `support_tickets` table at migration time would receive `requester_type=''` and `visibility_scope=''`, which:
- Would not match any valid enum value
- Would fail enum deserialization in EF (`string` → `TicketRequesterType` conversion)
- Would break any query filtering by `RequesterType` or `VisibilityScope`

**Severity:** Critical for any environment where the migration has already been applied. This is a pre-apply correction — no data has been migrated yet.

---

## 3. Migration Corrections

Changed `defaultValue` in `20260425022101_AddTicketOwnershipFields.cs`:
```
requester_type:  defaultValue: ""  →  defaultValue: "InternalUser"
visibility_scope: defaultValue: "" →  defaultValue: "Internal"
```

---

## 4. EF Model Configuration Corrections

Added to `SupportDbContext.cs`:
```csharp
ticket.Property(t => t.RequesterType)
    ...
    .HasDefaultValue("InternalUser");  // string literal, not enum value

ticket.Property(t => t.VisibilityScope)
    ...
    .HasDefaultValue("Internal");  // string literal, not enum value
```

**Why string literal and not enum value:** `HasDefaultValue(TicketRequesterType.InternalUser)` combined with `HasConversion<string>()` could cause EF to store the CLR integer value `0` in its model metadata (before conversion is applied), creating an inconsistency with the snapshot's `"InternalUser"` string. Using the string literal ensures all three layers — DbContext, migration, snapshot — are exactly consistent with each other.

---

## 5. Snapshot Corrections

Added `.HasDefaultValue("InternalUser")` and `.HasDefaultValue("Internal")` to the respective property blocks in `SupportDbContextModelSnapshot.cs`, matching the corrected DbContext config.

---

## 6. Validation Results

| Check | Result |
|-------|--------|
| Existing migrated rows receive requester_type='InternalUser' | PASS — explicit DB default now set |
| Existing migrated rows receive visibility_scope='Internal' | PASS — explicit DB default now set |
| New rows without explicit values get correct DB defaults | PASS — DB default + C# property default both = InternalUser/Internal |
| C# defaults match DB defaults | PASS — C# `= TicketRequesterType.InternalUser` aligns with DB default `'InternalUser'` |
| external_customer_id behavior unchanged | PASS — no change (nullable, no default) |
| No new public APIs added | PASS |
| No UI changes | PASS |

---

## 7. Build / Test Results

| Target        | Result | Errors | Warnings |
|---------------|--------|--------|----------|
| Support.Api   | PASS   | 0      | 11 (pre-existing) |
| Support.Tests | PASS   | 0      | 12 (pre-existing) |

---

## 8. Files Changed

| File | Change |
|------|--------|
| `Data/SupportDbContext.cs` | Added `HasDefaultValue(TicketRequesterType.InternalUser)` and `HasDefaultValue(TicketVisibilityScope.Internal)` to ticket entity config |
| `Data/Migrations/20260425022101_AddTicketOwnershipFields.cs` | Corrected `defaultValue` for `requester_type` and `visibility_scope` columns |
| `Data/Migrations/SupportDbContextModelSnapshot.cs` | Added `HasDefaultValue` to `RequesterType` and `VisibilityScope` property blocks |
| `analysis/SUP-TNT-02-01-report.md` | This file |

---

## 9. Known Gaps / Deferred Items

None introduced by this corrective iteration. Prior deferred items from SUP-TNT-02 remain unchanged.

---

## 10. Final Readiness Assessment

**READY.** All done criteria met:

| Criterion | Status |
|-----------|--------|
| requester_type DB default is InternalUser | PASS |
| visibility_scope DB default is Internal | PASS |
| C# defaults and DB defaults align | PASS |
| EF snapshot matches corrected defaults | PASS |
| No unrelated changes introduced | PASS |
| Build passes | PASS — 0 errors |
| Report complete | PASS — this document |
