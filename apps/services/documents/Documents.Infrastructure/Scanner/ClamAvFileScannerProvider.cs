using System.Net.Sockets;
using Documents.Domain.Enums;
using Documents.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Documents.Infrastructure.Scanner;

public sealed class ClamAvOptions
{
    public string Host        { get; set; } = "localhost";
    public int    Port        { get; set; } = 3310;
    public int    TimeoutMs   { get; set; } = 30_000;
    public int    ChunkSizeBytes { get; set; } = 2 * 1024 * 1024; // 2 MB

    public ClamAvCircuitBreakerOptions CircuitBreaker { get; set; } = new();

    /// <summary>
    /// Maximum number of hours the ClamAV virus-definition database may be old before the
    /// signature-freshness health check reports Degraded. Does NOT block scanning.
    /// </summary>
    public int SignatureMaxAgeHours { get; set; } = 24;

    /// <summary>
    /// ClamAV-level technical limit: files larger than this (MB) cannot be safely scanned.
    /// Used at startup validation to compare against the application-level MaxScannableFileSizeMb.
    /// </summary>
    public int MaxScannableFileSizeMb { get; set; } = 25;
}

/// <summary>
/// Configuration for the Polly advanced circuit breaker that wraps <see cref="ClamAvFileScannerProvider"/>.
/// Binds from Scanner:ClamAv:CircuitBreaker in appsettings.
/// </summary>
public sealed class ClamAvCircuitBreakerOptions
{
    /// <summary>Number of failures (within the sampling window) required before opening the circuit.</summary>
    public int FailureThreshold        { get; set; } = 5;

    /// <summary>How long (seconds) the circuit stays OPEN before entering HALF-OPEN.</summary>
    public int BreakDurationSeconds    { get; set; } = 30;

    /// <summary>Rolling window (seconds) over which the failure rate is measured.</summary>
    public int SamplingDurationSeconds { get; set; } = 60;

    /// <summary>Minimum number of calls within the sampling window before the circuit can trip.</summary>
    public int MinimumThroughput       { get; set; } = 5;
}

/// <summary>
/// Connects to a running ClamAV daemon (clamd) over TCP using the INSTREAM protocol.
/// See: https://linux.die.net/man/8/clamd – INSTREAM command.
/// </summary>
public sealed class ClamAvFileScannerProvider : IFileScannerProvider
{
    private readonly ClamAvOptions              _opts;
    private readonly ILogger<ClamAvFileScannerProvider> _log;

    public string ProviderName => "clamav";

    public ClamAvFileScannerProvider(
        IOptions<ClamAvOptions>             opts,
        ILogger<ClamAvFileScannerProvider>  log)
    {
        _opts = opts.Value;
        _log  = log;
    }

    public async Task<ScanResult> ScanAsync(
        Stream            content,
        string            fileName,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        string response;
        try
        {
            response = await ScanViaTcpAsync(content, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ClamAV TCP scan failed for file {FileName}", fileName);
            return new ScanResult
            {
                Status     = ScanStatus.Failed,
                Threats    = new(),
                DurationMs = (int)sw.ElapsedMilliseconds,
                EngineVersion = $"clamav/{_opts.Host}:{_opts.Port}",
            };
        }

        sw.Stop();
        _log.LogDebug("ClamAV raw response for {FileName}: {Response}", fileName, response);

        return ParseResponse(response, (int)sw.ElapsedMilliseconds);
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    private async Task<string> ScanViaTcpAsync(Stream content, CancellationToken ct)
    {
        using var tcp    = new TcpClient();
        tcp.ReceiveTimeout = _opts.TimeoutMs;
        tcp.SendTimeout    = _opts.TimeoutMs;

        await tcp.ConnectAsync(_opts.Host, _opts.Port, ct);

        await using var netStream = tcp.GetStream();

        // Write zINSTREAM\0 command (z-prefix = null-terminated)
        var cmd = System.Text.Encoding.ASCII.GetBytes("zINSTREAM\0");
        await netStream.WriteAsync(cmd, ct);

        // Stream file in chunks: [4-byte BE length][data] ... [4x 0x00]
        var buf = new byte[_opts.ChunkSizeBytes];
        int read;
        while ((read = await content.ReadAsync(buf, ct)) > 0)
        {
            var sizeBytes = new byte[4];
            sizeBytes[0] = (byte)(read >> 24);
            sizeBytes[1] = (byte)(read >> 16);
            sizeBytes[2] = (byte)(read >> 8);
            sizeBytes[3] = (byte)(read);
            await netStream.WriteAsync(sizeBytes, ct);
            await netStream.WriteAsync(buf.AsMemory(0, read), ct);
        }

        // Terminate stream with 4 zero bytes
        await netStream.WriteAsync(new byte[4], ct);
        await netStream.FlushAsync(ct);

        // Read the clamd response line
        using var sr = new System.IO.StreamReader(netStream, System.Text.Encoding.ASCII, leaveOpen: true);
        return (await sr.ReadLineAsync(ct))?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Parse clamd INSTREAM response.
    /// Clean:    "stream: OK"
    /// Infected: "stream: FOUND Virus.Name.Here"
    /// Error:    "stream: ERROR ..."
    /// </summary>
    private ScanResult ParseResponse(string response, int durationMs)
    {
        var engineLabel = $"clamav/{_opts.Host}:{_opts.Port}";

        if (response.EndsWith(": OK", StringComparison.OrdinalIgnoreCase))
        {
            return new ScanResult
            {
                Status        = ScanStatus.Clean,
                Threats       = new(),
                DurationMs    = durationMs,
                EngineVersion = engineLabel,
            };
        }

        if (response.Contains(" FOUND", StringComparison.OrdinalIgnoreCase))
        {
            // Format: "stream: FOUND Virus.Name" — extract threat name
            var parts  = response.Split(' ');
            var threat = parts.Length >= 3
                ? string.Join(' ', parts[2..^1])   // everything between FOUND and end
                : "Unknown";

            return new ScanResult
            {
                Status        = ScanStatus.Infected,
                Threats       = new() { threat },
                DurationMs    = durationMs,
                EngineVersion = engineLabel,
            };
        }

        // Any other response (ERROR or unexpected) → Failed
        _log.LogWarning("ClamAV unexpected response: {Response}", response);
        return new ScanResult
        {
            Status        = ScanStatus.Failed,
            Threats       = new(),
            DurationMs    = durationMs,
            EngineVersion = engineLabel,
        };
    }
}
