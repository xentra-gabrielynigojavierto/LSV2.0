# LS-LIENS-CASE-005 — Case Notes Backend & Persistence

**Status:** Complete  
**Date:** 2026-04-18  
**Spec:** Convert CASE-004 UI notes into fully persistent backend-driven feature  
**Reference Pattern:** FLOW-004 (LienTaskNote) mirrored for LienCaseNote

---

## 1. Executive Summary

CASE-005 converts the previously UI-only Case Notes feature (built in CASE-004 with TEMP_NOTES mock data and Zustand ephemeral store) into a fully persistent, backend-driven feature. The implementation follows the FLOW-004 Task Notes pattern exactly, introducing:

- A new `LienCaseNote` domain entity with content, category (general/internal/follow-up), isPinned, soft-delete, and ownership fields
- A new `liens_CaseNotes` MySQL table via EF migration `20260418172126_AddCaseNotes`
- Full CRUD + pin/unpin REST API: 6 endpoints under `/api/liens/cases/{caseId}/notes`
- 5 audit events: `liens.case_note.created/updated/deleted/pinned/unpinned`
- Case timeline hook: `liens.case.note_added` published on every new note
- New permission: `CaseNoteManage = "SYNQ_LIENS.case_note:manage"`
- Frontend `NotesTab` rewritten: TEMP_NOTES removed, Zustand caseNotes stripped, real API wired with loading/error states, inline edit/delete (owner-only hover controls), pin/unpin toggle

No regression to CASE-004 UI layout — category filters, sort order, search, pinned display, and timeline layout are all preserved with real data.

---

## 2. Codebase Assessment

### Pre-existing Case Notes UI (CASE-004)
| Location | Finding |
|---|---|
| `case-detail-client.tsx` line 1636 | `TEMP_NOTES: CaseNote[]` — 6 hardcoded mock notes |
| Line 1690 | `EMPTY_STORE_NOTES` constant for Zustand fallback |
| Lines 1693–1695 | `useLienStore(s => s.caseNotes[caseId])` + `addCaseNote` — ephemeral Zustand only |
| Line 1745 | `handleSubmit` called `addCaseNote` directly — no API call |
| Line 1871 | Banner: "Sample notes shown for UI review. Real notes will load from the API." |
| Line 1804 | Composer had "Not yet connected to API" label |

### Case Domain Gaps (pre-CASE-005)
| Item | State |
|---|---|
| `Case.Notes` scalar field | Present — preserved. This is the "Case Tracking Note" short text, separate from the activity thread |
| `ICaseService` | No note methods |
| `LiensPermissions` | No `CaseNoteManage` permission |
| `LiensDbContext` | No `CaseNotes` DbSet |

### Reference Patterns Used
- **Entity**: `LienTaskNote.cs` — mirrored with CaseId instead of TaskId, added Category and IsPinned
- **EF Config**: `LienTaskNoteConfiguration.cs` — same table prefix, index strategy, HasMaxLength(5000)
- **Service**: `LienTaskNoteService.cs` — same ownership enforcement, audit fire-and-forget
- **Endpoints**: `TaskNoteEndpoints.cs` — same minimal-API structure, 4 routes → 6 routes (added pin/unpin)
- **Repository**: `LienTaskNoteRepository.cs` — identical pattern

---

## 3. Files Changed

### New Files — Backend
| File | Purpose |
|---|---|
| `Liens.Domain/Entities/LienCaseNote.cs` | Domain entity with Create/Edit/Pin/Unpin/SoftDelete domain methods |
| `Liens.Domain/Enums/CaseNoteCategory.cs` | `general`, `internal`, `follow-up` constants + `All` validation set |
| `Liens.Application/DTOs/CaseNoteDto.cs` | `CaseNoteResponse`, `CreateCaseNoteRequest`, `UpdateCaseNoteRequest` |
| `Liens.Application/Repositories/ILienCaseNoteRepository.cs` | Repository interface: GetByCaseId, GetById, Add, Update |
| `Liens.Application/Interfaces/ILienCaseNoteService.cs` | Service interface: GetNotes, CreateNote, UpdateNote, DeleteNote, PinNote, UnpinNote |
| `Liens.Application/Services/LienCaseNoteService.cs` | Service implementation with tenant scoping, ownership enforcement, audit |
| `Liens.Infrastructure/Repositories/LienCaseNoteRepository.cs` | EF Core repository |
| `Liens.Infrastructure/Persistence/Configurations/LienCaseNoteConfiguration.cs` | EF table mapping, column constraints, 2 indexes |
| `Liens.Api/Endpoints/CaseNoteEndpoints.cs` | 6 minimal-API routes |

### Modified Files — Backend
| File | Change |
|---|---|
| `Liens.Domain/LiensPermissions.cs` | Added `CaseNoteManage = "SYNQ_LIENS.case_note:manage"` |
| `Liens.Infrastructure/Persistence/LiensDbContext.cs` | Added `DbSet<LienCaseNote> LienCaseNotes` |
| `Liens.Infrastructure/DependencyInjection.cs` | Registered `ILienCaseNoteRepository` + `ILienCaseNoteService` |
| `Liens.Api/Program.cs` | Added `app.MapCaseNoteEndpoints()` after `MapCaseEndpoints()` |

### New Files — Migration
| File | Purpose |
|---|---|
| `Liens.Infrastructure/Persistence/Migrations/20260418172126_AddCaseNotes.cs` | Creates `liens_CaseNotes` table + 2 indexes |
| `Liens.Infrastructure/Persistence/Migrations/20260418172126_AddCaseNotes.Designer.cs` | EF model snapshot |

### New Files — Frontend
| File | Purpose |
|---|---|
| `apps/web/src/lib/liens/lien-case-notes.types.ts` | `CaseNoteResponse`, `CreateCaseNoteRequest`, `UpdateCaseNoteRequest`, `CaseNoteCategory` |
| `apps/web/src/lib/liens/lien-case-notes.api.ts` | `lienCaseNotesApi` — list, create, update, remove, pin, unpin |
| `apps/web/src/lib/liens/lien-case-notes.service.ts` | `lienCaseNotesService` — typed wrappers for all API calls |

### Modified Files — Frontend
| File | Change |
|---|---|
| `apps/web/src/app/(platform)/lien/cases/[id]/case-detail-client.tsx` | Import added; `TEMP_NOTES` array removed; `EMPTY_STORE_NOTES` removed; Zustand `caseNotes`/`addCaseNote` removed; `NotesTab` fully rewritten to use real API |

---

## 4. Database / Schema Changes

### Migration: `20260418172126_AddCaseNotes`

**Table:** `liens_CaseNotes`

| Column | Type | Constraints |
|---|---|---|
| Id | char(36) | PK, NOT NULL |
| CaseId | char(36) | NOT NULL |
| TenantId | char(36) | NOT NULL |
| Content | varchar(5000) | NOT NULL, MaxLength(5000) |
| Category | varchar(20) | NOT NULL, Default: `general` |
| IsPinned | tinyint(1) | NOT NULL, Default: `false` |
| CreatedByUserId | char(36) | NOT NULL |
| CreatedByName | varchar(250) | NOT NULL |
| IsEdited | tinyint(1) | NOT NULL, Default: `false` |
| IsDeleted | tinyint(1) | NOT NULL, Default: `false` |
| CreatedAtUtc | datetime(6) | NOT NULL |
| UpdatedAtUtc | datetime(6) | NULL |

**Indexes:**

| Name | Columns | Purpose |
|---|---|---|
| `IX_CaseNotes_TenantId_CaseId_CreatedAt` | (TenantId, CaseId, CreatedAtUtc) | Primary query index — tenant-scoped list |
| `IX_CaseNotes_CaseId_IsDeleted` | (CaseId, IsDeleted) | Fast soft-delete filter |

**Applied:** Yes — `dotnet ef database update` ran successfully.

**Preserved:** `Case.Notes` scalar field remains untouched on the `liens_Cases` table.

---

## 5. API Changes

### New Endpoints — `/api/liens/cases/{caseId}/notes`

| Method | Route | Permission | Handler |
|---|---|---|---|
| GET | `/api/liens/cases/{caseId}/notes` | `CaseRead` | Returns all non-deleted notes for the case, ordered by `CreatedAtUtc ASC` |
| POST | `/api/liens/cases/{caseId}/notes` | `CaseNoteManage` | Creates a note; validates content length ≤ 5000 chars |
| PUT | `/api/liens/cases/{caseId}/notes/{noteId}` | `CaseNoteManage` | Updates content/category; enforces owner-only |
| DELETE | `/api/liens/cases/{caseId}/notes/{noteId}` | `CaseNoteManage` | Soft-deletes; enforces owner-only |
| POST | `/api/liens/cases/{caseId}/notes/{noteId}/pin` | `CaseNoteManage` | Sets `IsPinned = true` |
| POST | `/api/liens/cases/{caseId}/notes/{noteId}/unpin` | `CaseNoteManage` | Sets `IsPinned = false` |

### Response Shape (`CaseNoteResponse`)
```json
{
  "id": "guid",
  "caseId": "guid",
  "content": "string (≤5000)",
  "category": "general | internal | follow-up",
  "isPinned": false,
  "createdByUserId": "guid",
  "createdByName": "string",
  "isEdited": false,
  "createdAtUtc": "2026-04-18T...",
  "updatedAtUtc": null
}
```

### Request Shapes
```json
// POST (CreateCaseNoteRequest)
{ "content": "string", "category": "general", "createdByName": "string" }

// PUT (UpdateCaseNoteRequest)
{ "content": "string", "category": "internal" }
```

### Validation
- `content` required, ≤ 5000 characters (enforced in service layer)
- `category` defaults to `general` if unrecognized value supplied
- Case must belong to the tenant (service calls `ICaseRepository.GetByIdAsync`)
- Edit/delete: actor must be the note's `CreatedByUserId` — `UnauthorizedAccessException` thrown otherwise
- Soft-deleted notes return 200 on re-delete (idempotent)

---

## 6. UI Changes

### NotesTab — Rewrites
| Before | After |
|---|---|
| `TEMP_NOTES` hardcoded array (6 mock notes) | Real API call to GET `/api/liens/cases/{id}/notes` |
| Zustand `storeNotes` + `addCaseNote` | `useState<CaseNoteResponse[]>` loaded from API |
| "Sample notes shown for UI review" amber banner | Removed |
| "Not yet connected to API" label in composer | Removed |
| Submit → `addCaseNote()` | Submit → `lienCaseNotesService.createNote()` |
| No loading/error states | Loading spinner, error message with Retry button |
| No edit/delete controls | Hover controls: pin icon (all), edit + delete icons (own notes only) |
| No edit mode | Inline edit form with category selector, textarea, Save/Cancel |
| Pinned state from mock data only | Persistent pin/unpin via API (POST .../pin and .../unpin) |

### Preserved UI Elements
- Timeline layout (avatar → bubble, date separators, connector line)
- Category filter pills (All / General / Internal / Follow-Up)
- Sort order toggle (Newest First / Oldest First)
- Full-text search across note content and author name
- Category color coding (blue/purple/amber)
- Pinned badge on pinned notes
- Edited badge on edited notes (now wired to real `isEdited` field)
- Composer: textarea expand on focus, category selector, Submit/Cancel

---

## 7. Permissions / Security

### New Permission
```csharp
public const string CaseNoteManage = "SYNQ_LIENS.case_note:manage";
```

### Endpoint Authorization
| Action | Required Permission |
|---|---|
| Read notes | `CaseRead` |
| Create / edit / delete / pin / unpin | `CaseNoteManage` |

### Ownership Enforcement
- Edit: `note.CreatedByUserId != actorUserId` → `UnauthorizedAccessException`
- Delete: `note.CreatedByUserId != actorUserId` → `UnauthorizedAccessException`
- Pin/Unpin: any user with `CaseNoteManage` can pin (not ownership-restricted)

### Tenant Isolation
- All repository queries include `tenantId` filter
- Service validates case belongs to tenant before any note operation
- `LienCaseNoteConfiguration` indexes include `TenantId`

---

## 8. Audit Integration

### Events Published

| Event Type | Trigger | entityType | Description |
|---|---|---|---|
| `liens.case_note.created` | Note added | `LienCaseNote` | "Note added to case" |
| `liens.case_note.updated` | Note edited | `LienCaseNote` | "Note updated on case" |
| `liens.case_note.deleted` | Note soft-deleted | `LienCaseNote` | "Note deleted from case" |
| `liens.case_note.pinned` | Note pinned | `LienCaseNote` | "Note pinned on case" |
| `liens.case_note.unpinned` | Note unpinned | `LienCaseNote` | "Note unpinned on case" |

### Metadata Included
- `tenantId` (via `_audit.Publish(... tenantId: ...)`)
- `actorUserId`
- `entityId` = noteId
- `metadata` = `"caseId={caseId}"` (no full content stored in audit)

---

## 9. Case Timeline Integration

When a note is created, a secondary audit event is published:

```csharp
_audit.Publish(
    eventType:   "liens.case.note_added",
    action:      "update",
    description: $"Note added to case by {note.CreatedByName}",
    tenantId:    tenantId,
    actorUserId: actorUserId,
    entityType:  "Case",
    entityId:    caseId.ToString(),
    metadata:    $"noteId={note.Id}");
```

- **entityType = "Case"** — surfaces in the case timeline
- Author name included in description
- Full note content is NOT included (follows spec: no flooding the timeline)
- Only fires on create, not on edit/delete/pin

---

## 10. Validation Results

### Backend Build
| Check | Result |
|---|---|
| `dotnet build Liens.Api/Liens.Api.csproj` | ✅ Build succeeded — 0 errors, 1 pre-existing warning (MSB3277 JWT version conflict) |
| EF migration generated | ✅ `20260418172126_AddCaseNotes` |
| EF migration applied | ✅ `dotnet ef database update` succeeded |

### Frontend Build
| Check | Result |
|---|---|
| `tsc --noEmit` (web) — new files | ✅ 0 errors in `lien-case-notes.*` and `case-detail-client.tsx` |
| Pre-existing errors in `lien-task-notes.service.ts` | ⚠️ Pre-existing (not introduced by CASE-005) |
| Application startup | ✅ Next.js web + control-center `✓ Ready` |

### Functional Checklist

| Check | Status |
|---|---|
| `GET /api/liens/cases/{id}/notes` returns empty array for new case | ✅ |
| `POST /api/liens/cases/{id}/notes` creates note, persists in DB | ✅ |
| UI reload → note remains (persisted, not ephemeral) | ✅ |
| `PUT` edit works, `IsEdited` flag set | ✅ |
| `DELETE` soft-deletes, note disappears from UI | ✅ |
| Pin persists (`IsPinned = true`), note floated to top | ✅ |
| Unpin persists | ✅ |
| Category filter still works | ✅ |
| Sort order still works | ✅ |
| Search still works | ✅ |
| Loading state shows spinner | ✅ |
| Error state shows retry button | ✅ |
| Owner-only edit/delete controls (hover) | ✅ |
| Non-owner cannot edit/delete (backend enforcement) | ✅ |
| TEMP_NOTES banner removed | ✅ |
| Zustand addCaseNote/caseNotes fully removed | ✅ |
| Audit events `liens.case_note.*` fired | ✅ |
| Timeline event `liens.case.note_added` fired on create | ✅ |
| Multi-tenant scoping enforced at all layers | ✅ |

---

## 11. Known Gaps / Risks

| Item | Notes |
|---|---|
| `lien-task-notes.service.ts` pre-existing TS errors | These existed before CASE-005 and are unrelated. The `ApiResponse<T>` return type mismatch in that file should be fixed in a separate task. |
| `useSession().session?.userId` matching | The `createdByUserId` comparison to `currentUserId` from session requires the session userId to match the Guid string stored in the API. If token claims differ from the DB Guid, the edit/delete controls won't render for the owner (safe failure — backend also enforces). |
| Pin permission not owner-restricted | Any user with `CaseNoteManage` can pin any note. This is intentional (mirrors CASE-005 spec: "Pin Notes" is not restricted to owner). |
| Content not summarized in audit | Per spec, full note content is not stored in audit events. The `metadata` field contains only `caseId` reference. |

---

## 12. Run Instructions

### Backend
```bash
# Build
cd apps/services/liens
dotnet build Liens.Api/Liens.Api.csproj

# Migration already applied; to re-apply from scratch:
dotnet ef database update --project Liens.Infrastructure --startup-project Liens.Api
```

### Test Endpoints
```bash
# List notes (replace IDs and token)
curl -H "Authorization: Bearer {token}" \
  https://{env}/lien/api/liens/cases/{caseId}/notes

# Create note
curl -X POST -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"content":"Test note","category":"general","createdByName":"Jane Doe"}' \
  https://{env}/lien/api/liens/cases/{caseId}/notes

# Pin note
curl -X POST -H "Authorization: Bearer {token}" \
  https://{env}/lien/api/liens/cases/{caseId}/notes/{noteId}/pin
```

### Frontend
Navigate to any Case → Notes tab. The composer, filters, sort, and timeline all use live API data. No mocks or Zustand state for notes.
