# LS-REPORTS-04-001 — Report Export Engine (CSV / XLSX / PDF)

## Status: COMPLETE

## Objective
Enable report execution results to be exported as downloadable files in CSV, XLSX, and PDF formats.

---

## Execution Log

| Step | Description | Status |
|------|-------------|--------|
| 1 | Create report file | Done |
| 2 | Add Export DTOs | Done |
| 3 | Create exporter interface | Done |
| 4 | Implement CSV exporter | Done |
| 5 | Implement XLSX exporter | Done |
| 6 | Implement PDF exporter | Done |
| 7 | Create export service | Done |
| 8 | Integrate execution service | Done |
| 9 | Add API endpoint | Done |
| 10 | Add audit events | Done |
| 11 | Validate everything | Done |
| 12 | Finalize report | Done |

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `reports/src/Reports.Application/Export/DTOs/ExportReportRequest.cs` | Created | Export request DTO with ExportFormat enum (CSV, XLSX, PDF) |
| `reports/src/Reports.Application/Export/DTOs/ExportReportResponse.cs` | Created | Export response DTO (ExportId, FileName, ContentType, FileSize, FileContent) |
| `reports/src/Reports.Application/Export/IReportExportService.cs` | Created | Export service interface |
| `reports/src/Reports.Application/Export/ReportExportService.cs` | Created | Export service implementation — orchestrates execution + export + audit |
| `reports/src/Reports.Contracts/Export/IReportExporter.cs` | Created | Exporter abstraction (IReportExporter, ExportContext, ExportResult) |
| `reports/src/Reports.Infrastructure/Exporters/CsvReportExporter.cs` | Created | CSV exporter with proper escaping, UTF-8 BOM |
| `reports/src/Reports.Infrastructure/Exporters/XlsxReportExporter.cs` | Created | XLSX exporter using ClosedXML with typed cells |
| `reports/src/Reports.Infrastructure/Exporters/PdfReportExporter.cs` | Created | PDF exporter using QuestPDF with table layout |
| `reports/src/Reports.Api/Endpoints/ExportEndpoints.cs` | Created | POST /api/v1/report-exports endpoint |
| `reports/src/Reports.Application/Audit/AuditEventFactory.cs` | Modified | Added ExportStarted, ExportCompleted, ExportFailed factory methods |
| `reports/src/Reports.Application/DependencyInjection.cs` | Modified | Registered IReportExportService |
| `reports/src/Reports.Infrastructure/DependencyInjection.cs` | Modified | Registered 3 IReportExporter implementations |
| `reports/src/Reports.Api/Program.cs` | Modified | Added MapExportEndpoints() |
| `reports/src/Reports.Infrastructure/Reports.Infrastructure.csproj` | Modified | Added ClosedXML 0.102.3 and QuestPDF 2024.3.0 |

---

## Export Formats Summary

| Format | Library | Content-Type | Features |
|--------|---------|-------------|----------|
| CSV | System.Text (built-in) | text/csv; charset=utf-8 | Comma-delimited, header row, proper escaping, UTF-8 BOM |
| XLSX | ClosedXML 0.102.3 | application/vnd.openxmlformats-officedocument.spreadsheetml.sheet | Single worksheet, header row, typed cells, auto-width columns |
| PDF | QuestPDF 2024.3.0 (Community) | application/pdf | Landscape A4, header with metadata, table layout, page numbers |

---

## API Endpoint

### POST `/api/v1/report-exports`

**Request Body:**
```json
{
  "tenantId": "string",
  "templateId": "guid",
  "productCode": "string",
  "organizationType": "string",
  "format": "CSV | XLSX | PDF",
  "parametersJson": "string (optional)",
  "requestedByUserId": "string",
  "useOverride": true
}
```

**Response:** File download with appropriate Content-Type header.

**Error Responses:**
- 400: Invalid format, missing fields, file too large
- 404: Template not found, no published version
- 500: Execution or export failure

---

## Architecture Flow

```
POST /api/v1/report-exports
  → ExportEndpoints (API layer)
    → IReportExportService.ExportReportAsync()
      → Validate request
      → Audit: report.export.started
      → IReportExecutionService.ExecuteReportAsync()
        → (full template → assignment → version → override → query pipeline)
      → Map ReportExecutionResponse → TabularResultSet
      → IReportExporter.ExportAsync() (resolved by format)
      → Enforce 10MB file size guardrail
      → Audit: report.export.completed | report.export.failed
    → Results.File() (binary download)
```

---

## Audit Events

| Event Type | Entity Type | Trigger |
|-----------|-------------|---------|
| report.export.started | ReportExport | Export request received, before execution |
| report.export.completed | ReportExport | File generated successfully |
| report.export.failed | ReportExport | Execution failure, export generation error, or file size exceeded |

Metadata includes: templateId, format, rowCount, fileSize, reason (on failure).

---

## Guardrails

| Guardrail | Value | Enforcement |
|-----------|-------|-------------|
| Max rows | 500 | Reused from ReportExecutionService (MaxRowCap) |
| Max file size | 10 MB | Enforced in ReportExportService after generation |
| Format validation | CSV, XLSX, PDF | Enum validation + exporter lookup |
| Request validation | All required fields | Same pattern as execution service |

---

## Build Status

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Tests: 3/3 passed (Api, Application, Infrastructure).

---

## Decisions

1. **Exporter as IEnumerable<IReportExporter>**: Registered all three exporters in DI and resolved by FormatName match. This allows easy addition of new formats without modifying the service.
2. **File download via Results.File()**: The export endpoint returns a binary file download rather than JSON-wrapped base64. The ExportReportResponse DTO carries metadata for internal use while the API layer returns the actual file.
3. **QuestPDF Community License**: Using the Community license tier (free for revenue < $1M). License is set in the PdfReportExporter at export time.
4. **Reuse execution pipeline**: Export service delegates to IReportExecutionService.ExecuteReportAsync() rather than duplicating template/assignment/version/override/query logic.
5. **File naming**: `{TemplateCode}_{yyyyMMdd_HHmmss}.{ext}` — standardized across all formats.
6. **CSV UTF-8 BOM**: Included for Excel compatibility on Windows.

---

## Known Gaps

1. **No persistent storage**: Export files are generated in-memory and returned directly. No S3/blob storage. This is by design per story scope.
2. **No scheduling**: Export is synchronous on-demand only. Future story for scheduled exports.
3. **No email/FTP delivery**: Out of scope per story requirements.
4. **PDF styling is basic**: Simple table layout with header and page numbers. Advanced templating deferred.
5. **XLSX single worksheet**: No multi-sheet support. Could be added if templates define sections.

---

## Final Summary

LS-REPORTS-04-001 is complete. The Report Export Engine enables executed reports to be exported as CSV, XLSX, or PDF files through a single API endpoint (`POST /api/v1/report-exports`). The architecture follows the existing service patterns: clean exporter abstraction (`IReportExporter`), full integration with the execution pipeline (no logic duplication), config-driven format resolution, audit event coverage for the full export lifecycle, and enforced guardrails (row cap via execution, 10MB file size cap). Build passes with 0 errors, all tests pass.
