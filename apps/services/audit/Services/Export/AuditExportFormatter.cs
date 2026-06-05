using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Services.Export;

/// <summary>
/// Stateless streaming file formatter for audit event exports.
///
/// Supports three output formats:
///
///   Json   — Full JSON envelope:
///              { "exportId": "...", "exportedAtUtc": "...", "format": "Json",
///                "records": [ {...}, {...} ] }
///              Best for programmatic consumption; one round-trip download.
///
///   Ndjson — Newline-delimited JSON (one record per line, no envelope).
///              Best for streaming ingest into data pipelines (Spark, BigQuery, etc.).
///
///   Csv    — Flat CSV with header row.
///              Best for spreadsheet consumers and compliance officers.
///              Nested fields (BeforeJson, AfterJson, MetadataJson) are omitted
///              from CSV to preserve readability; use Json/Ndjson for full fidelity.
///
/// All formats stream records through an <c>IAsyncEnumerable</c> so memory
/// consumption stays bounded regardless of result-set size.
///
/// Format control is driven by <see cref="ExportWriterOptions"/>:
///   - IncludeHashes          → include Hash / PreviousHash fields.
///   - IncludeStateSnapshots  → include BeforeJson / AfterJson fields.
///   - IncludeTags            → include Tags field.
/// When a flag is false, the corresponding field is null in JSON/NDJSON and the
/// column is omitted from CSV headers and rows.
/// </summary>
public static class AuditExportFormatter
{
    // ── Serializer options ────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented               = false,
    };

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Write all <paramref name="records"/> to <paramref name="stream"/> in the
    /// requested <paramref name="format"/>. Returns the number of records written.
    ///
    /// The stream is left open; callers are responsible for flushing and disposing.
    /// </summary>
    public static Task<long> WriteAsync(
        Stream                             stream,
        IAsyncEnumerable<AuditEventRecord> records,
        Guid                               exportId,
        string                             format,
        ExportWriterOptions                opts,
        CancellationToken                  ct = default)
    {
        return format.ToUpperInvariant() switch
        {
            "CSV"    => WriteCsvAsync(stream, records, opts, ct),
            "NDJSON" => WriteNdjsonAsync(stream, records, opts, ct),
            _        => WriteJsonAsync(stream, records, exportId, opts, ct),
        };
    }

    // ── JSON ──────────────────────────────────────────────────────────────────

    private static async Task<long> WriteJsonAsync(
        Stream                             stream,
        IAsyncEnumerable<AuditEventRecord> records,
        Guid                               exportId,
        ExportWriterOptions                opts,
        CancellationToken                  ct)
    {
        await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

        // Envelope header
        await writer.WriteAsync(
            $"{{\"exportId\":\"{exportId}\"," +
            $"\"exportedAtUtc\":\"{DateTimeOffset.UtcNow:o}\"," +
            $"\"format\":\"Json\"," +
            $"\"records\":[");

        long  count = 0;
        bool  first = true;

        await foreach (var record in records.WithCancellation(ct))
        {
            if (!first)
                await writer.WriteAsync(",");

            first = false;

            var row  = ToRow(record, opts);
            var json = JsonSerializer.Serialize(row, _jsonOpts);
            await writer.WriteAsync(json);

            count++;
        }

        // Envelope footer
        await writer.WriteAsync("]}");
        await writer.FlushAsync(ct);

        return count;
    }

    // ── NDJSON ────────────────────────────────────────────────────────────────

    private static async Task<long> WriteNdjsonAsync(
        Stream                             stream,
        IAsyncEnumerable<AuditEventRecord> records,
        ExportWriterOptions                opts,
        CancellationToken                  ct)
    {
        await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

        long count = 0;

        await foreach (var record in records.WithCancellation(ct))
        {
            var row  = ToRow(record, opts);
            var json = JsonSerializer.Serialize(row, _jsonOpts);
            await writer.WriteLineAsync(json.AsMemory(), ct);
            count++;
        }

        await writer.FlushAsync(ct);
        return count;
    }

    // ── CSV ───────────────────────────────────────────────────────────────────

    private static async Task<long> WriteCsvAsync(
        Stream                             stream,
        IAsyncEnumerable<AuditEventRecord> records,
        ExportWriterOptions                opts,
        CancellationToken                  ct)
    {
        await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

        // Header
        await writer.WriteLineAsync(BuildCsvHeader(opts).AsMemory(), ct);

        long count = 0;

        await foreach (var record in records.WithCancellation(ct))
        {
            await writer.WriteLineAsync(ToCsvLine(record, opts).AsMemory(), ct);
            count++;
        }

        await writer.FlushAsync(ct);
        return count;
    }

    // ── Projection helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Project a single <see cref="AuditEventRecord"/> into the export row shape,
    /// applying field-inclusion rules from <paramref name="opts"/>.
    /// </summary>
    private static ExportRow ToRow(AuditEventRecord r, ExportWriterOptions opts) =>
        new()
        {
            AuditId           = r.AuditId.ToString(),
            EventId           = r.EventId?.ToString(),
            EventType         = r.EventType,
            EventCategory     = r.EventCategory.ToString(),
            SourceSystem      = r.SourceSystem,
            SourceService     = r.SourceService,
            SourceEnvironment = r.SourceEnvironment,
            PlatformId        = r.PlatformId?.ToString(),
            TenantId          = r.TenantId,
            OrganizationId    = r.OrganizationId,
            UserScopeId       = r.UserScopeId,
            ScopeType         = r.ScopeType.ToString(),
            ActorId           = r.ActorId,
            ActorType         = r.ActorType.ToString(),
            ActorName         = r.ActorName,
            ActorIpAddress    = r.ActorIpAddress,
            ActorUserAgent    = r.ActorUserAgent,
            EntityType        = r.EntityType,
            EntityId          = r.EntityId,
            Action            = r.Action,
            Description       = r.Description,
            BeforeJson        = opts.IncludeStateSnapshots ? r.BeforeJson : null,
            AfterJson         = opts.IncludeStateSnapshots ? r.AfterJson  : null,
            MetadataJson      = r.MetadataJson,
            CorrelationId     = r.CorrelationId,
            RequestId         = r.RequestId,
            SessionId         = r.SessionId,
            VisibilityScope   = r.VisibilityScope.ToString(),
            Severity          = r.Severity.ToString(),
            OccurredAtUtc     = r.OccurredAtUtc.ToString("o"),
            RecordedAtUtc     = r.RecordedAtUtc.ToString("o"),
            Hash              = opts.IncludeHashes ? r.Hash : null,
            PreviousHash      = opts.IncludeHashes ? r.PreviousHash : null,
            IdempotencyKey    = r.IdempotencyKey,
            IsReplay          = r.IsReplay,
            Tags              = opts.IncludeTags ? r.TagsJson : null,
        };

    // ── CSV formatting ────────────────────────────────────────────────────────

    private static string BuildCsvHeader(ExportWriterOptions opts)
    {
        var cols = new List<string>
        {
            "auditId","eventId","eventType","eventCategory",
            "sourceSystem","sourceService","sourceEnvironment",
            "platformId","tenantId","organizationId","userScopeId","scopeType",
            "actorId","actorType","actorName","actorIpAddress","actorUserAgent",
            "entityType","entityId","action","description",
            "metadataJson","correlationId","requestId","sessionId",
            "visibilityScope","severity","occurredAtUtc","recordedAtUtc",
            "idempotencyKey","isReplay"
        };

        if (opts.IncludeStateSnapshots) { cols.Add("beforeJson"); cols.Add("afterJson"); }
        if (opts.IncludeHashes)         { cols.Add("hash");        cols.Add("previousHash"); }
        if (opts.IncludeTags)           cols.Add("tags");

        return string.Join(",", cols);
    }

    private static string ToCsvLine(AuditEventRecord r, ExportWriterOptions opts)
    {
        var fields = new List<string?>
        {
            r.AuditId.ToString(),
            r.EventId?.ToString(),
            r.EventType,
            r.EventCategory.ToString(),
            r.SourceSystem,
            r.SourceService,
            r.SourceEnvironment,
            r.PlatformId?.ToString(),
            r.TenantId,
            r.OrganizationId,
            r.UserScopeId,
            r.ScopeType.ToString(),
            r.ActorId,
            r.ActorType.ToString(),
            r.ActorName,
            r.ActorIpAddress,
            r.ActorUserAgent,
            r.EntityType,
            r.EntityId,
            r.Action,
            r.Description,
            r.MetadataJson,
            r.CorrelationId,
            r.RequestId,
            r.SessionId,
            r.VisibilityScope.ToString(),
            r.Severity.ToString(),
            r.OccurredAtUtc.ToString("o"),
            r.RecordedAtUtc.ToString("o"),
            r.IdempotencyKey,
            r.IsReplay.ToString().ToLowerInvariant(),
        };

        if (opts.IncludeStateSnapshots) { fields.Add(r.BeforeJson); fields.Add(r.AfterJson); }
        if (opts.IncludeHashes)         { fields.Add(r.Hash);        fields.Add(r.PreviousHash); }
        if (opts.IncludeTags)           fields.Add(r.TagsJson);

        return string.Join(",", fields.Select(CsvEscape));
    }

    /// <summary>
    /// RFC 4180-compliant CSV field escaping.
    /// Fields containing commas, double-quotes, or line breaks are wrapped in
    /// double-quotes; embedded double-quotes are doubled.
    /// Null values are serialised as empty fields.
    /// </summary>
    private static string CsvEscape(string? value)
    {
        if (value is null)                                                           return "";
        if (!value.Contains(',') && !value.Contains('"')
            && !value.Contains('\n') && !value.Contains('\r'))                       return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}

// ── Supporting types ──────────────────────────────────────────────────────────

/// <summary>
/// Export-friendly projection of a single <see cref="AuditEventRecord"/>.
/// All enum values are serialised as their string names for readability.
/// Null fields are omitted from JSON/NDJSON output.
/// </summary>
internal sealed class ExportRow
{
    public required string  AuditId           { get; init; }
    public string?          EventId           { get; init; }
    public required string  EventType         { get; init; }
    public required string  EventCategory     { get; init; }
    public required string  SourceSystem      { get; init; }
    public string?          SourceService     { get; init; }
    public string?          SourceEnvironment { get; init; }
    public string?          PlatformId        { get; init; }
    public string?          TenantId          { get; init; }
    public string?          OrganizationId    { get; init; }
    public string?          UserScopeId       { get; init; }
    public required string  ScopeType         { get; init; }
    public string?          ActorId           { get; init; }
    public required string  ActorType         { get; init; }
    public string?          ActorName         { get; init; }
    public string?          ActorIpAddress    { get; init; }
    public string?          ActorUserAgent    { get; init; }
    public string?          EntityType        { get; init; }
    public string?          EntityId          { get; init; }
    public required string  Action            { get; init; }
    public required string  Description       { get; init; }

    // ── Conditional fields ────────────────────────────────────────────────────
    // Null when the corresponding IncludeXxx flag is false; omitted from JSON
    // output by the DefaultIgnoreCondition=WhenWritingNull serializer option.

    public string?          BeforeJson        { get; init; }
    public string?          AfterJson         { get; init; }
    public string?          MetadataJson      { get; init; }
    public string?          CorrelationId     { get; init; }
    public string?          RequestId         { get; init; }
    public string?          SessionId         { get; init; }
    public required string  VisibilityScope   { get; init; }
    public required string  Severity          { get; init; }
    public required string  OccurredAtUtc     { get; init; }
    public required string  RecordedAtUtc     { get; init; }
    public string?          Hash              { get; init; }
    public string?          PreviousHash      { get; init; }
    public string?          IdempotencyKey    { get; init; }
    public required bool    IsReplay          { get; init; }
    public string?          Tags              { get; init; }
}

/// <summary>
/// Controls which optional field groups are included in the export file.
/// Constructed by <see cref="Services.AuditExportService"/> from the caller's
/// <see cref="DTOs.Export.ExportRequest"/> and authorization scope.
/// </summary>
public sealed record ExportWriterOptions(
    bool IncludeHashes,
    bool IncludeStateSnapshots,
    bool IncludeTags);
