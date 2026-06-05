using Documents.Domain.Enums;
using Documents.Domain.Interfaces;

namespace Documents.Infrastructure.Scanner;

/// <summary>
/// Pass-through scanner — all files pass with ScanStatus.Skipped.
/// Used when FILE_SCANNER_PROVIDER=none.
/// </summary>
public sealed class NullScannerProvider : IFileScannerProvider
{
    public string ProviderName => "none";

    public Task<ScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct = default)
        => Task.FromResult(new ScanResult
        {
            Status    = ScanStatus.Skipped,
            Threats   = new(),
            DurationMs = 0,
        });
}
