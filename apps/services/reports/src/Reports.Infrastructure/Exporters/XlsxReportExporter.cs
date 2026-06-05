using ClosedXML.Excel;
using Reports.Contracts.Adapters;
using Reports.Contracts.Export;

namespace Reports.Infrastructure.Exporters;

public sealed class XlsxReportExporter : IReportExporter
{
    public string FormatName => "XLSX";

    public Task<ExportResult> ExportAsync(TabularResultSet data, ExportContext ctx, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();
        var sheetName = string.IsNullOrWhiteSpace(ctx.TemplateName)
            ? "Report"
            : ctx.TemplateName.Length > 31
                ? ctx.TemplateName[..31]
                : ctx.TemplateName;

        var worksheet = workbook.Worksheets.Add(sheetName);
        var orderedColumns = data.Columns.OrderBy(c => c.Order).ToList();

        for (var col = 0; col < orderedColumns.Count; col++)
        {
            var cell = worksheet.Cell(1, col + 1);
            cell.Value = orderedColumns[col].Label;
            cell.Style.Font.Bold = true;
        }

        for (var rowIdx = 0; rowIdx < data.Rows.Count; rowIdx++)
        {
            ct.ThrowIfCancellationRequested();
            var row = data.Rows[rowIdx];
            for (var col = 0; col < orderedColumns.Count; col++)
            {
                row.TryGetValue(orderedColumns[col].Key, out var val);
                var cell = worksheet.Cell(rowIdx + 2, col + 1);
                SetCellValue(cell, val, orderedColumns[col].DataType);
            }
        }

        worksheet.Columns().AdjustToContents(1, Math.Min(data.Rows.Count + 1, 100));

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var bytes = ms.ToArray();
        var fileName = $"{ctx.TemplateCode}_{ctx.GeneratedAtUtc:yyyyMMdd_HHmmss}.xlsx";

        return Task.FromResult(new ExportResult
        {
            FileContent = bytes,
            FileName = fileName,
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileSize = bytes.Length
        });
    }

    private static void SetCellValue(IXLCell cell, object? val, string dataType)
    {
        if (val is null) return;

        switch (val)
        {
            case int i: cell.Value = i; break;
            case long l: cell.Value = l; break;
            case decimal dec: cell.Value = (double)dec; break;
            case double dbl: cell.Value = dbl; break;
            case float f: cell.Value = (double)f; break;
            case DateTime dt: cell.Value = dt; cell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss"; break;
            case DateTimeOffset dto: cell.Value = dto.DateTime; cell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss"; break;
            case bool b: cell.Value = b; break;
            default: cell.Value = val.ToString() ?? ""; break;
        }
    }
}
