using System.Globalization;
using System.Net.Sockets;
using Documents.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Documents.Infrastructure.Scanner;

/// <summary>
/// Snapshot of a ClamAV signature-freshness query.
/// Populated by <see cref="ClamAvSignatureFreshnessMonitor"/>.
/// </summary>
public sealed class ClamAvSignatureInfo
{
    /// <summary>True when the VERSION command succeeded and was parsable.</summary>
    public bool      Success      { get; init; }

    /// <summary>Raw version string returned by clamd, e.g. "ClamAV 0.103.8/26830/Fri Jul 14 09:20:13 2023".</summary>
    public string    RawVersion   { get; init; } = string.Empty;

    /// <summary>Numeric ClamAV daily signature database version (middle segment).</summary>
    public int       DbVersion    { get; init; }

    /// <summary>Parsed date of the signature database from the VERSION response.</summary>
    public DateTime? DbDate       { get; init; }

    /// <summary>Age of the signature database in seconds (null when date could not be parsed).</summary>
    public double?   AgeSeconds   { get; init; }

    /// <summary>Error message when <see cref="Success"/> is false.</summary>
    public string?   ErrorMessage { get; init; }

    /// <summary>UTC timestamp when this snapshot was obtained.</summary>
    public DateTime  QueriedAt    { get; init; }
}

/// <summary>
/// Singleton service that queries the ClamAV VERSION command over TCP and caches the result
/// for a configurable period (default 5 minutes).
///
/// The VERSION command returns: ClamAV &lt;engine&gt;/&lt;db-version&gt;/&lt;db-date&gt;
/// Example: "ClamAV 0.103.8/26830/Fri Jul 14 09:20:13 2023"
///
/// This is a read-only observability component — it does NOT affect scan behavior.
/// </summary>
public sealed class ClamAvSignatureFreshnessMonitor
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly ClamAvOptions                              _opts;
    private readonly ILogger<ClamAvSignatureFreshnessMonitor>  _log;
    private readonly SemaphoreSlim                              _lock = new(1, 1);

    private ClamAvSignatureInfo _cached = new()
    {
        Success   = false,
        QueriedAt = DateTime.MinValue,
    };

    public ClamAvSignatureFreshnessMonitor(
        IOptions<ClamAvOptions>                            opts,
        ILogger<ClamAvSignatureFreshnessMonitor>           log)
    {
        _opts = opts.Value;
        _log  = log;
    }

    /// <summary>
    /// Returns the latest signature freshness snapshot, refreshing the cache when stale.
    /// Never throws — returns a failed snapshot on connectivity errors.
    /// </summary>
    public async Task<ClamAvSignatureInfo> GetAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (_cached.QueriedAt != DateTime.MinValue && now - _cached.QueriedAt < CacheTtl)
            return _cached;

        await _lock.WaitAsync(ct);
        try
        {
            now = DateTime.UtcNow;
            if (_cached.QueriedAt != DateTime.MinValue && now - _cached.QueriedAt < CacheTtl)
                return _cached;

            _cached = await QueryVersionAsync(ct);
            UpdateMetric(_cached);
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private async Task<ClamAvSignatureInfo> QueryVersionAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMilliseconds(Math.Min(_opts.TimeoutMs, 5_000)));

        try
        {
            using var tcp = new TcpClient();
            tcp.SendTimeout    = Math.Min(_opts.TimeoutMs, 5_000);
            tcp.ReceiveTimeout = Math.Min(_opts.TimeoutMs, 5_000);

            await tcp.ConnectAsync(_opts.Host, _opts.Port, cts.Token);

            await using var stream = tcp.GetStream();
            var cmd = System.Text.Encoding.ASCII.GetBytes("zVERSION\0");
            await stream.WriteAsync(cmd, cts.Token);
            await stream.FlushAsync(cts.Token);

            using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
            var raw = (await reader.ReadLineAsync(cts.Token))?.Trim('\0').Trim() ?? string.Empty;

            return Parse(raw);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "ClamAV VERSION query failed ({Host}:{Port}) — signature freshness unknown",
                _opts.Host, _opts.Port);

            return new ClamAvSignatureInfo
            {
                Success      = false,
                ErrorMessage = ex.Message,
                QueriedAt    = DateTime.UtcNow,
            };
        }
    }

    /// <summary>
    /// Parses the clamd VERSION response.
    /// Format: "ClamAV &lt;engine&gt;/&lt;db-version&gt;/&lt;db-date&gt;"
    /// Example: "ClamAV 0.103.8/26830/Fri Jul 14 09:20:13 2023"
    /// </summary>
    internal static ClamAvSignatureInfo Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new ClamAvSignatureInfo
            {
                Success      = false,
                RawVersion   = raw,
                ErrorMessage = "Empty VERSION response",
                QueriedAt    = DateTime.UtcNow,
            };
        }

        var parts = raw.Split('/');
        if (parts.Length < 3)
        {
            return new ClamAvSignatureInfo
            {
                Success      = false,
                RawVersion   = raw,
                ErrorMessage = $"Unexpected VERSION format: '{raw}'",
                QueriedAt    = DateTime.UtcNow,
            };
        }

        int.TryParse(parts[1].Trim(), out var dbVersion);

        // The db-date segment looks like "Fri Jul 14 09:20:13 2023" or "Fri Jul  4 09:20:13 2023"
        // (ctime-style, %a %b %e %T %Y where %e is space-padded).
        // Normalise multiple spaces to single space, then try a set of formats.
        var datePart = System.Text.RegularExpressions.Regex.Replace(parts[2].Trim(), @"\s+", " ");

        var formats = new[]
        {
            "ddd MMM d HH:mm:ss yyyy",
            "ddd MMM dd HH:mm:ss yyyy",
        };

        DateTime? dbDate = null;
        if (DateTime.TryParseExact(datePart, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowInnerWhite | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            dbDate = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }
        else if (DateTime.TryParse(datePart, CultureInfo.InvariantCulture,
                     DateTimeStyles.AssumeUniversal, out var fallback))
        {
            dbDate = DateTime.SpecifyKind(fallback, DateTimeKind.Utc);
        }

        double? ageSeconds = dbDate.HasValue
            ? (DateTime.UtcNow - dbDate.Value).TotalSeconds
            : null;

        return new ClamAvSignatureInfo
        {
            Success    = true,
            RawVersion = raw,
            DbVersion  = dbVersion,
            DbDate     = dbDate,
            AgeSeconds = ageSeconds,
            QueriedAt  = DateTime.UtcNow,
        };
    }

    private static void UpdateMetric(ClamAvSignatureInfo info)
    {
        if (info.Success && info.AgeSeconds.HasValue)
            ScanMetrics.ClamAvSignatureAgeSeconds.Set(info.AgeSeconds.Value);
    }
}
