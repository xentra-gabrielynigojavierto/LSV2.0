using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Reports.Contracts.Configuration;
using Reports.Contracts.Adapters;

namespace Reports.Infrastructure.Adapters;

public sealed class LiensReportDataQueryAdapter : IReportDataQueryAdapter
{
    private readonly string _connectionString;
    private readonly LiensDataSettings _settings;
    private readonly ILogger<LiensReportDataQueryAdapter> _log;

    public LiensReportDataQueryAdapter(
        IConfiguration configuration,
        IOptions<LiensDataSettings> settings,
        ILogger<LiensReportDataQueryAdapter> log)
    {
        _connectionString = configuration.GetConnectionString("LiensDb") ?? string.Empty;
        _settings = settings.Value;
        _log = log;
    }

    public bool SupportsProduct(string productCode) =>
        string.Equals(productCode, "LIENS", StringComparison.OrdinalIgnoreCase);

    public async Task<AdapterResult<TabularResultSet>> ExecuteQueryAsync(ReportQueryContext context, CancellationToken ct)
    {
        _log.LogInformation(
            "LiensReportDataQueryAdapter: executing query for tenant={TenantId} template={TemplateCode} maxRows={MaxRows}",
            context.TenantId, context.TemplateCode, context.MaxRows);

        try
        {
            var maxRows = Math.Min(context.MaxRows, _settings.MaxRows);

            var (sql, columns) = BuildQuery(context, maxRows);

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            await using var command = new MySqlCommand(sql, connection)
            {
                CommandTimeout = _settings.QueryTimeoutSeconds,
            };
            command.Parameters.AddWithValue("@tenantId", context.TenantId);
            command.Parameters.AddWithValue("@maxRows", maxRows);

            await using var reader = await command.ExecuteReaderAsync(ct);

            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var colName = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);

                    if (value is DateTime dt)
                        value = dt.ToString("yyyy-MM-dd");
                    else if (value is DateTimeOffset dto)
                        value = dto.ToString("yyyy-MM-dd");

                    row[colName] = value;
                }
                rows.Add(row);
            }

            var wasTruncated = rows.Count >= maxRows;

            _log.LogInformation(
                "LiensReportDataQueryAdapter: query completed — rows={RowCount} truncated={Truncated}",
                rows.Count, wasTruncated);

            return AdapterResult<TabularResultSet>.Ok(new TabularResultSet
            {
                Columns = columns,
                Rows = rows,
                TotalRowCount = rows.Count,
                WasTruncated = wasTruncated,
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "LiensReportDataQueryAdapter: query failed for tenant={TenantId} template={TemplateCode}",
                context.TenantId, context.TemplateCode);
            return AdapterResult<TabularResultSet>.Fail("LiensQueryError", ex.Message, isRetryable: true);
        }
    }

    private static (string Sql, List<TabularColumn> Columns) BuildQuery(ReportQueryContext context, int maxRows)
    {
        var templateCode = context.TemplateCode?.ToUpperInvariant() ?? "";

        return templateCode switch
        {
            "LIENS_SUMMARY" or "LIEN_SUMMARY" => (
                @"SELECT
                    l.Id AS lienId,
                    l.LienNumber AS lienNumber,
                    l.SubjectFirstName AS subjectFirstName,
                    l.SubjectLastName AS subjectLastName,
                    l.Status AS status,
                    l.LienType AS lienType,
                    l.TotalCharges AS totalCharges,
                    l.BalanceDue AS balanceDue,
                    l.IncidentDate AS incidentDate,
                    l.CreatedAt AS createdDate
                FROM Liens l
                WHERE l.TenantId = @tenantId
                ORDER BY l.CreatedAt DESC
                LIMIT @maxRows",
                new List<TabularColumn>
                {
                    new() { Key = "lienId", Label = "Lien ID", DataType = "string", Order = 1 },
                    new() { Key = "lienNumber", Label = "Lien Number", DataType = "string", Order = 2 },
                    new() { Key = "subjectFirstName", Label = "First Name", DataType = "string", Order = 3 },
                    new() { Key = "subjectLastName", Label = "Last Name", DataType = "string", Order = 4 },
                    new() { Key = "status", Label = "Status", DataType = "string", Order = 5 },
                    new() { Key = "lienType", Label = "Lien Type", DataType = "string", Order = 6 },
                    new() { Key = "totalCharges", Label = "Total Charges", DataType = "decimal", Order = 7 },
                    new() { Key = "balanceDue", Label = "Balance Due", DataType = "decimal", Order = 8 },
                    new() { Key = "incidentDate", Label = "Incident Date", DataType = "date", Order = 9 },
                    new() { Key = "createdDate", Label = "Created Date", DataType = "date", Order = 10 },
                }
            ),
            "LIENS_AGING" or "LIEN_AGING" => (
                @"SELECT
                    l.Id AS lienId,
                    l.LienNumber AS lienNumber,
                    CONCAT(l.SubjectFirstName, ' ', l.SubjectLastName) AS subjectName,
                    l.Status AS status,
                    l.BalanceDue AS balanceDue,
                    l.CreatedAt AS createdDate,
                    DATEDIFF(CURDATE(), l.CreatedAt) AS ageDays
                FROM Liens l
                WHERE l.TenantId = @tenantId AND l.Status NOT IN ('Closed', 'Withdrawn')
                ORDER BY l.CreatedAt ASC
                LIMIT @maxRows",
                new List<TabularColumn>
                {
                    new() { Key = "lienId", Label = "Lien ID", DataType = "string", Order = 1 },
                    new() { Key = "lienNumber", Label = "Lien Number", DataType = "string", Order = 2 },
                    new() { Key = "subjectName", Label = "Subject Name", DataType = "string", Order = 3 },
                    new() { Key = "status", Label = "Status", DataType = "string", Order = 4 },
                    new() { Key = "balanceDue", Label = "Balance Due", DataType = "decimal", Order = 5 },
                    new() { Key = "createdDate", Label = "Created Date", DataType = "date", Order = 6 },
                    new() { Key = "ageDays", Label = "Age (Days)", DataType = "int", Order = 7 },
                }
            ),
            _ => (
                @"SELECT
                    l.Id AS lienId,
                    l.LienNumber AS lienNumber,
                    l.SubjectFirstName AS subjectFirstName,
                    l.SubjectLastName AS subjectLastName,
                    l.Status AS status,
                    l.LienType AS lienType,
                    l.TotalCharges AS totalCharges,
                    l.BalanceDue AS balanceDue,
                    l.IncidentDate AS incidentDate,
                    l.CreatedAt AS createdDate
                FROM Liens l
                WHERE l.TenantId = @tenantId
                ORDER BY l.CreatedAt DESC
                LIMIT @maxRows",
                new List<TabularColumn>
                {
                    new() { Key = "lienId", Label = "Lien ID", DataType = "string", Order = 1 },
                    new() { Key = "lienNumber", Label = "Lien Number", DataType = "string", Order = 2 },
                    new() { Key = "subjectFirstName", Label = "First Name", DataType = "string", Order = 3 },
                    new() { Key = "subjectLastName", Label = "Last Name", DataType = "string", Order = 4 },
                    new() { Key = "status", Label = "Status", DataType = "string", Order = 5 },
                    new() { Key = "lienType", Label = "Lien Type", DataType = "string", Order = 6 },
                    new() { Key = "totalCharges", Label = "Total Charges", DataType = "decimal", Order = 7 },
                    new() { Key = "balanceDue", Label = "Balance Due", DataType = "decimal", Order = 8 },
                    new() { Key = "incidentDate", Label = "Incident Date", DataType = "date", Order = 9 },
                    new() { Key = "createdDate", Label = "Created Date", DataType = "date", Order = 10 },
                }
            ),
        };
    }
}
