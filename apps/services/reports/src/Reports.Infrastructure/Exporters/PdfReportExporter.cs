using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Reports.Contracts.Adapters;
using Reports.Contracts.Export;

namespace Reports.Infrastructure.Exporters;

public sealed class PdfReportExporter : IReportExporter
{
    public string FormatName => "PDF";

    public Task<ExportResult> ExportAsync(TabularResultSet data, ExportContext ctx, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var orderedColumns = data.Columns.OrderBy(c => c.Order).ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(col =>
                {
                    col.Item().Text(ctx.TemplateName).FontSize(14).Bold();
                    col.Item().Text($"Generated: {ctx.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC | Tenant: {ctx.TenantId} | Rows: {data.TotalRowCount}").FontSize(7).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingBottom(8).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in orderedColumns)
                            columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var col in orderedColumns)
                        {
                            header.Cell()
                                .Background(Colors.Grey.Lighten3)
                                .Padding(4)
                                .Text(col.Label)
                                .FontSize(7)
                                .Bold();
                        }
                    });

                    foreach (var row in data.Rows)
                    {
                        ct.ThrowIfCancellationRequested();
                        foreach (var col in orderedColumns)
                        {
                            row.TryGetValue(col.Key, out var val);
                            table.Cell()
                                .BorderBottom(0.25f)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(3)
                                .Text(FormatValue(val))
                                .FontSize(7);
                        }
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        var bytes = document.GeneratePdf();
        var fileName = $"{ctx.TemplateCode}_{ctx.GeneratedAtUtc:yyyyMMdd_HHmmss}.pdf";

        return Task.FromResult(new ExportResult
        {
            FileContent = bytes,
            FileName = fileName,
            ContentType = "application/pdf",
            FileSize = bytes.Length
        });
    }

    private static string FormatValue(object? val)
    {
        if (val is null) return "";
        if (val is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss");
        if (val is DateTimeOffset dto) return dto.ToString("yyyy-MM-dd HH:mm:ss");
        if (val is decimal dec) return dec.ToString("N2");
        if (val is double dbl) return dbl.ToString("N2");
        return val.ToString() ?? "";
    }
}
