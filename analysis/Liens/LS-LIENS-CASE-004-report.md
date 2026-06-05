# LS-LIENS-CASE-004 — Case Notes Tab UI

## Objective
Build the Case Detail Notes tab as a full-width, timeline-style interface for human-entered case commentary and internal collaboration.

## Scope
- Notes tab body content only
- Full-width layout (no LayoutSplit)
- Timeline-style note display with avatars and date separators
- Note composer with category tagging
- Search and filter by category
- Sort order toggle (newest/oldest)
- TEMP fallback data for UI review

## Architecture

### Layout
- Full-width (`max-w-4xl mx-auto`) — no LayoutSplit
- Content-heavy readable workspace

### Note Data Model
- `CaseNote` interface: `id`, `text`, `author`, `timestamp`, `category?` (general/internal/follow-up), `pinned?`
- 6 TEMP notes with varied categories and 1 pinned note
- User-added notes via `useLienStore.addCaseNote()` merged with TEMP data

### Note Composer
- Avatar-integrated composer at top
- Multiline textarea that expands on focus
- Category selector (General / Internal / Follow-Up)
- "Not yet connected to API" label on category (notes save to local Zustand store, not backend)
- Cancel and Add Note actions

### Timeline Display
- Vertical timeline line connecting notes
- Color-coded author avatars with initials (hash-based color selection for consistency)
- Date group separators (e.g., "Monday, Apr 14")
- Pinned notes float to top of list
- Relative timestamps (Just now, 2h ago, 3d ago) with full timestamp on hover
- Category badges for Internal and Follow-Up notes
- Pinned indicator badge

### Filters
- Search: across note text and author name
- Category filter: segmented control (All / General / Internal / Follow-Up)
- Sort toggle: Newest First / Oldest First

### Empty States
- Filtered empty: "No notes match the current filters" + Clear filters link
- No notes: "No notes yet" + prompt to use composer

## Files Changed
| File | Change |
|------|--------|
| `apps/web/src/app/(platform)/lien/cases/[id]/case-detail-client.tsx` | Replaced `NotesPanel` usage with new `NotesTab` component. Removed unused `NotesPanel` import, `EMPTY_NOTES` constant, and `caseNotes` variable. |

## Validation Results
- TypeScript: Clean build, no errors
- Notes render in timeline format with avatars and date separators
- Composer expands/collapses correctly, notes saved to Zustand store
- Search filters across text and author
- Category filter works with segmented control
- Sort toggle works (newest/oldest)
- Pinned note stays at top
- No regressions to other tabs

## Current User Resolution
- Author name derived from session email (`session.email.split('@')[0]` → title-cased)
- Session has no `displayName` or `firstName/lastName` — email-based derivation is best available
- When real API is connected, backend should return proper author names

## Code Review Adjustments
- **Category now persists**: Extended `addCaseNote` in lien-store to accept `{ category, author }` options. Composer category selection now actually persists to the note record.
- **Author name passed**: `handleSubmit` now passes derived `authorName` instead of relying on store hardcoded `'Current User'`.
- **Full-width layout**: Removed `max-w-4xl mx-auto` constraint — Notes tab now uses true full-width layout as specified.
- **Category reset**: Composer resets category to 'general' after submission.

## Remaining Gaps
- Notes persist only in Zustand store (in-memory) — page reload loses user-added notes
- No backend API for notes CRUD
- Pin/unpin action not yet interactive (display only on TEMP data)
- No edit/delete actions on individual notes (future enhancement)
