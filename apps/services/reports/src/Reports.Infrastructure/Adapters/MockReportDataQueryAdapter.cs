using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;

namespace Reports.Infrastructure.Adapters;

public sealed class MockReportDataQueryAdapter : IReportDataQueryAdapter
{
    private readonly ILogger<MockReportDataQueryAdapter> _log;

    public MockReportDataQueryAdapter(ILogger<MockReportDataQueryAdapter> log) => _log = log;

    public bool SupportsProduct(string productCode)
    {
        return productCode is "LIENS" or "FUND" or "CARECONNECT" or "BILLING" or "RX" or "PAYOUT";
    }

    public Task<AdapterResult<TabularResultSet>> ExecuteQueryAsync(ReportQueryContext context, CancellationToken ct)
    {
        _log.LogInformation(
            "MockReportDataQueryAdapter: Executing query for tenant={TenantId} product={ProductCode} template={TemplateCode} maxRows={MaxRows}",
            context.TenantId, context.ProductCode, context.TemplateCode, context.MaxRows);

        var columns = GenerateColumnsForProduct(context.ProductCode);
        var rows = GenerateRowsForProduct(context.ProductCode, context.TenantId, Math.Min(context.MaxRows, 25));

        var result = new TabularResultSet
        {
            Columns = columns,
            Rows = rows,
            TotalRowCount = rows.Count,
            WasTruncated = false
        };

        return Task.FromResult(AdapterResult<TabularResultSet>.Ok(result));
    }

    private static List<TabularColumn> GenerateColumnsForProduct(string productCode)
    {
        return productCode switch
        {
            "LIENS" => new List<TabularColumn>
            {
                new() { Key = "lienId", Label = "Lien ID", DataType = "string", Order = 1 },
                new() { Key = "claimantName", Label = "Claimant Name", DataType = "string", Order = 2 },
                new() { Key = "providerName", Label = "Provider Name", DataType = "string", Order = 3 },
                new() { Key = "amount", Label = "Amount", DataType = "decimal", Order = 4 },
                new() { Key = "status", Label = "Status", DataType = "string", Order = 5 },
                new() { Key = "filedDate", Label = "Filed Date", DataType = "date", Order = 6 },
            },
            "FUND" => new List<TabularColumn>
            {
                new() { Key = "fundingId", Label = "Funding ID", DataType = "string", Order = 1 },
                new() { Key = "applicantName", Label = "Applicant Name", DataType = "string", Order = 2 },
                new() { Key = "requestedAmount", Label = "Requested Amount", DataType = "decimal", Order = 3 },
                new() { Key = "approvedAmount", Label = "Approved Amount", DataType = "decimal", Order = 4 },
                new() { Key = "status", Label = "Status", DataType = "string", Order = 5 },
                new() { Key = "requestDate", Label = "Request Date", DataType = "date", Order = 6 },
            },
            "CARECONNECT" => new List<TabularColumn>
            {
                new() { Key = "referralId", Label = "Referral ID", DataType = "string", Order = 1 },
                new() { Key = "patientName", Label = "Patient Name", DataType = "string", Order = 2 },
                new() { Key = "referringProvider", Label = "Referring Provider", DataType = "string", Order = 3 },
                new() { Key = "receivingProvider", Label = "Receiving Provider", DataType = "string", Order = 4 },
                new() { Key = "status", Label = "Status", DataType = "string", Order = 5 },
                new() { Key = "referralDate", Label = "Referral Date", DataType = "date", Order = 6 },
            },
            _ => new List<TabularColumn>
            {
                new() { Key = "id", Label = "ID", DataType = "string", Order = 1 },
                new() { Key = "name", Label = "Name", DataType = "string", Order = 2 },
                new() { Key = "value", Label = "Value", DataType = "string", Order = 3 },
                new() { Key = "createdDate", Label = "Created Date", DataType = "date", Order = 4 },
            },
        };
    }

    private static List<Dictionary<string, object?>> GenerateRowsForProduct(string productCode, string tenantId, int rowCount)
    {
        var rows = new List<Dictionary<string, object?>>();
        var statuses = new[] { "Active", "Pending", "Completed", "Closed" };

        for (int i = 1; i <= rowCount; i++)
        {
            var row = productCode switch
            {
                "LIENS" => new Dictionary<string, object?>
                {
                    ["lienId"] = $"LN-{tenantId}-{i:D4}",
                    ["claimantName"] = $"Claimant {i}",
                    ["providerName"] = $"Provider {(i % 5) + 1}",
                    ["amount"] = Math.Round(1000m + i * 250.50m, 2),
                    ["status"] = statuses[i % statuses.Length],
                    ["filedDate"] = DateTimeOffset.UtcNow.AddDays(-i * 3).ToString("yyyy-MM-dd"),
                },
                "FUND" => new Dictionary<string, object?>
                {
                    ["fundingId"] = $"FN-{tenantId}-{i:D4}",
                    ["applicantName"] = $"Applicant {i}",
                    ["requestedAmount"] = Math.Round(5000m + i * 1000m, 2),
                    ["approvedAmount"] = Math.Round(4500m + i * 900m, 2),
                    ["status"] = statuses[i % statuses.Length],
                    ["requestDate"] = DateTimeOffset.UtcNow.AddDays(-i * 2).ToString("yyyy-MM-dd"),
                },
                "CARECONNECT" => new Dictionary<string, object?>
                {
                    ["referralId"] = $"RF-{tenantId}-{i:D4}",
                    ["patientName"] = $"Patient {i}",
                    ["referringProvider"] = $"Dr. Referring {(i % 4) + 1}",
                    ["receivingProvider"] = $"Dr. Receiving {(i % 3) + 1}",
                    ["status"] = statuses[i % statuses.Length],
                    ["referralDate"] = DateTimeOffset.UtcNow.AddDays(-i).ToString("yyyy-MM-dd"),
                },
                _ => new Dictionary<string, object?>
                {
                    ["id"] = $"GEN-{tenantId}-{i:D4}",
                    ["name"] = $"Record {i}",
                    ["value"] = $"Value-{i}",
                    ["createdDate"] = DateTimeOffset.UtcNow.AddDays(-i).ToString("yyyy-MM-dd"),
                },
            };
            rows.Add(row);
        }
        return rows;
    }
}
