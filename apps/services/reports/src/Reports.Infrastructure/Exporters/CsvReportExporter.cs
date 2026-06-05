using System.Globalization;
using System.Text;
using Reports.Contracts.Adapters;
using Reports.Contracts.Export;

namespace Reports.Infrastructure.Exporters;

public sealed class CsvReportExporter : IReportExporter
{
    public string FormatName => "CSV";

    public Task<ExportResult> ExportAsync(TabularResultSet data, ExportContext ctx, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        var orderedColumns = data.Columns.OrderBy(c => c.Order).ToList();

        sb.AppendLine(string.Join(",", orderedColumns.Select(c => EscapeCsv(c.Label))));

        foreach (var row in data.Rows)
        {
            ct.ThrowIfCancellationRequested();
            var values = orderedColumns.Select(col =>
            {
                row.TryGetValue(col.Key, out var val);
                return EscapeCsv(FormatValue(val));
            });
            sb.AppendLine(string.Join(",", values));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"{ctx.TemplateCode}_{ctx.GeneratedAtUtc:yyyyMMdd_HHmmss}.csv";

        return Task.FromResult(new ExportResult
        {
            FileContent = bytes,
            FileName = fileName,
            ContentType = "text/csv; charset=utf-8",
            FileSize = bytes.Length
        });
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        value = NeutralizeFormula(value);

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static string NeutralizeFormula(string value)
    {
        if (value.Length > 0 && (value[0] == '=' || value[0] == '+' || value[0] == '-' || value[0] == '@' || value[0] == '\t' || value[0] == '\r'))
        {
            return $"'{value}";
        }
        return value;
    }

    private static string FormatValue(object? val)
    {
        if (val is null) return "";
        if (val is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        if (val is DateTimeOffset dto) return dto.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        if (val is decimal dec) return dec.ToString(CultureInfo.InvariantCulture);
        if (val is double dbl) return dbl.ToString(CultureInfo.InvariantCulture);
        if (val is float flt) return flt.ToString(CultureInfo.InvariantCulture);
        return val.ToString() ?? "";
    }
}
