using Documents.Domain.Entities;
using Documents.Domain.Enums;
using Documents.Domain.Interfaces;
using Documents.Application.Exceptions;
using Microsoft.Extensions.Logging;

namespace Documents.Application.Services;

public sealed class ScanService
{
    private readonly IFileScannerProvider _scanner;
    private readonly ILogger<ScanService> _log;

    public ScanService(IFileScannerProvider scanner, ILogger<ScanService> log)
    {
        _scanner = scanner;
        _log     = log;
    }

    /// <summary>
    /// Scan a file stream. Returns the scan result.
    /// Throws InfectedFileException if malware is detected.
    /// </summary>
    public async Task<ScanStatusUpdate> ScanAsync(
        Stream  content,
        string  fileName,
        CancellationToken ct = default)
    {
        _log.LogInformation("Scanning file {FileName} with provider {Provider}", fileName, _scanner.ProviderName);

        var sw     = System.Diagnostics.Stopwatch.StartNew();
        var result = await _scanner.ScanAsync(content, fileName, ct);
        sw.Stop();

        _log.LogInformation(
            "Scan complete: {Status}, threats={Count}, duration={Ms}ms",
            result.Status, result.Threats.Count, (int)sw.ElapsedMilliseconds);

        if (result.Status == ScanStatus.Infected)
        {
            _log.LogWarning("Infected file rejected: threatCount={Count}", result.Threats.Count);
            throw new InfectedFileException($"File rejected: {result.Threats.Count} threat(s) detected");
        }

        return new ScanStatusUpdate
        {
            ScanStatus        = result.Status,
            ScanCompletedAt   = DateTime.UtcNow,
            ScanDurationMs    = result.DurationMs,
            ScanThreats       = result.Threats,
            ScanEngineVersion = result.EngineVersion,
        };
    }

    /// <summary>
    /// Enforce scan gate for document access. Throws ScanBlockedException if access is blocked.
    /// </summary>
    public void EnforceCleanScan(Document doc, bool requireClean = false)
    {
        if (doc.ScanStatus == ScanStatus.Infected)
        {
            throw new ScanBlockedException($"Access denied: file scan status is INFECTED. Only CLEAN files may be accessed.");
        }

        if (requireClean && doc.ScanStatus is ScanStatus.Pending or ScanStatus.Failed or ScanStatus.Skipped)
        {
            throw new ScanBlockedException($"Access denied: file scan status is {doc.ScanStatus.ToString().ToUpperInvariant()}.");
        }
    }

    public void EnforceCleanScan(DocumentVersion version, bool requireClean = false)
    {
        if (version.ScanStatus == ScanStatus.Infected)
        {
            throw new ScanBlockedException($"Access denied: file scan status is INFECTED. Only CLEAN files may be accessed.");
        }

        if (requireClean && version.ScanStatus is ScanStatus.Pending or ScanStatus.Failed or ScanStatus.Skipped)
        {
            throw new ScanBlockedException($"Access denied: file scan status is {version.ScanStatus.ToString().ToUpperInvariant()}.");
        }
    }
}
