# LS-REPORTS-09-002 — Formatting Application Layer

## Objective
Apply existing formatting configuration to execution results and export outputs so formatted values appear consistently in the UI viewer, CSV, XLSX, and PDF exports.

## Scope
- Centralized `ReportFormattingService` (stateless, reusable, no DB access)
- Currency, Number, Percentage, Date, Boolean, Text format types
- Integration into execution pipeline (after calculated fields)
- Integration into export pipeline (CSV, XLSX, PDF)
- UI DataGrid updated to prefer formatted values
- Graceful error handling (fallback to raw value on failure)

## Out of Scope
- Conditional formatting (colors, highlighting)
- Localization engine
- UI redesign
- Advanced templating
- Client-side formatting logic

---

## Execution Log

| Step | Status |
|------|--------|
| 1. Create report file | COMPLETED |
| 2. Implement ReportFormattingService | COMPLETED |
| 3. Parse formatting config | COMPLETED (uses existing FormattingConfigParser) |
| 4. Apply formatting in execution pipeline | COMPLETED |
| 5. Apply formatting in export pipeline | COMPLETED |
| 6. Update UI to display formatted values | COMPLETED |
| 7. Validate build | COMPLETED |
| 8. Finalize report | COMPLETED |

---

## Files Modified

| File | Change |
|------|--------|
| `Reports.Application/Formatting/ReportFormattingService.cs` | **NEW** — Stateless formatting engine with currency, number, percentage, date, boolean, text formatters |
| `Reports.Application/Execution/ReportExecutionService.cs` | Added formatting step after calculated fields; builds FormattedValues per row |
| `Reports.Application/Execution/DTOs/ReportRowResponse.cs` | Added `FormattedValues` dictionary alongside raw `Values` |
| `Reports.Application/Export/ReportExportService.cs` | Merges FormattedValues into export rows so exporters receive pre-formatted data |
| `Reports.Application/Formulas/FormulaEvaluator.cs` | Fixed pre-existing `ColumnDefinition` → `TabularColumn` type reference |
| `apps/web/src/lib/reports/reports.types.ts` | Added `formattedValues?: Record<string, string>` to `ReportRowDto` |
| `apps/web/src/components/reports/data-grid.tsx` | Prefers `formattedValues[col]` over raw `formatCell()` when available |

---

## Formatting Rules Implemented

| Type | Behavior | Default |
|------|----------|---------|
| **Currency** | Prefix + thousand separators + decimal places | `$`, 2 decimals. Example: `1234.5` → `$1,234.50` |
| **Number** | Thousand separators + decimal precision | 0 decimals. Example: `1234567` → `1,234,567` |
| **Percentage** | Multiply by 100, append suffix, apply precision | 1 decimal, `%` suffix. Example: `0.856` → `85.6%` |
| **Date** | Apply format string to DateTime/DateTimeOffset/string | `yyyy-MM-dd`. Example: `2026-04-15T10:30:00` → `2026-04-15` |
| **Boolean** | Map true/false to custom labels | `Yes`/`No`. Example: `true` → `Yes` |
| **Text** | Pass-through with null fallback | Empty string on null |

All types support `NullLabel` for custom null display.

---

## Execution Integration Summary

**Pipeline flow:** Raw Data → Calculated Fields → **Formatting** → Response

- `FormattingConfigParser.Parse()` extracts rules from `definition.FormattingConfigJson`
- `ReportFormattingService.FormatRows()` processes all rows in-memory, producing a parallel list of `Dictionary<string, string>`
- Each `ReportRowResponse` carries both `Values` (raw) and `FormattedValues` (formatted strings)
- Raw values are never mutated — formatting is purely additive
- Only columns with matching formatting rules get formatted entries; other columns are unaffected

---

## Export Integration Summary

- `ReportExportService` checks for `FormattedValues` on execution results
- When present, formatted string values are merged into the row dictionaries before passing to exporters
- All three exporters (CSV, XLSX, PDF) receive pre-formatted data — no exporter-specific formatting logic needed
- Formatted values replace raw values for formatted columns; unformatted columns keep raw values
- This ensures consistency: UI and exports show identical formatted output

---

## UI Integration Summary

- `DataGrid` component updated: `row.formattedValues?.[col.name] ?? formatCell(row.values[col.name], col.dataType)`
- When `formattedValues` exists for a column, it takes priority over the client-side `formatCell()` fallback
- Sorting still operates on raw `values` (not formatted strings), preserving correct sort behavior
- No new client-side formatting logic added — server-side formatting is authoritative

---

## Validation Results

| Check | Result |
|-------|--------|
| .NET build (Reports.Api) | 0 errors, 0 warnings |
| TypeScript build (web) | 0 errors |
| Unknown format types | Safely ignored (FallbackFormat) |
| Null values | Return `NullLabel` or empty string |
| Non-numeric values for currency/number/percentage | Fall back to raw `ToString()` |
| Invalid date strings | Fall back to raw `ToString()` |
| Formatting exception | Caught, logged as warning, returns raw value |
| Raw values preserved | Yes — `Values` dict never mutated |
| Formula pipeline unaffected | Yes — formatting runs after formulas |

---

## Decisions Made

1. **Percentage multiply-by-100**: Values are multiplied by 100 before display. The assumption is that raw data stores percentages as decimals (e.g., `0.85` = 85%). Documented in formatting rules.

2. **Dual-value response**: `ReportRowResponse` carries both `Values` (raw) and `FormattedValues` (strings). This preserves raw data for sorting/calculations while providing display-ready strings.

3. **Export uses merged values**: Rather than modifying exporter interfaces, formatted values are merged into the row dictionaries before passing to exporters. This keeps the `IReportExporter` interface stable.

4. **Server-side authoritative formatting**: The UI `DataGrid` defers to server-provided `formattedValues`. No duplicate formatting logic on the client. The existing `formatCell()` function serves as a fallback for reports without formatting config.

5. **Pre-existing FormulaEvaluator fix**: Fixed `ColumnDefinition` → `TabularColumn` type mismatch that was a latent bug from LS-REPORTS-09-001.

---

## Issues Encountered

1. **Pre-existing type mismatch**: `FormulaEvaluator.cs` referenced `Reports.Contracts.Adapters.ColumnDefinition` which doesn't exist — should be `TabularColumn`. Fixed as part of this task.

2. **NuGet analyzer cache**: NuGet package analyzer DLLs were missing from the local cache (xunit.analyzers, AWS SDK analyzers). Resolved after `dotnet restore`.

---

## Known Gaps

1. **XLSX native formatting**: Currently XLSX exports receive pre-formatted strings. A future enhancement could apply native Excel number formats (e.g., `$#,##0.00`) so cells remain numeric and formulas work. Current approach is consistent but treats formatted values as text in Excel.

2. **Locale support**: All formatting uses `CultureInfo.InvariantCulture` (English number/date conventions). Locale-aware formatting would require a localization engine (out of scope).

3. **Conditional formatting**: Colors, highlighting, and conditional rules are not supported. Only value transformation is implemented.

4. **Override-level formatting**: Formatting config is currently only sourced from Views (`activeView?.FormattingConfigJson`). Overrides don't have their own formatting config field.

---

## Final Summary

LS-REPORTS-09-002 is complete. The formatting application layer is centralized in `ReportFormattingService`, integrated into the execution pipeline after calculated fields, and propagated to all export formats. The UI displays server-provided formatted values when available, with graceful fallback to client-side formatting. All builds pass with 0 errors.
