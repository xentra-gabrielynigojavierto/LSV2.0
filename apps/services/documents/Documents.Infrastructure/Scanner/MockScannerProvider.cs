using Documents.Domain.Enums;
using Documents.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace Documents.Infrastructure.Scanner;

public sealed class MockScannerOptions
{
    /// <summary>Override scan result for testing. Values: "clean", "infected", "failed".</summary>
    public string MockResult { get; set; } = "clean";
}

/// <summary>
/// Configurable scanner for testing.
/// Used when FILE_SCANNER_PROVIDER=mock.
/// </summary>
public sealed class MockScannerProvider : IFileScannerProvider
{
    private readonly MockScannerOptions _opts;

    public string ProviderName => "mock";

    public MockScannerProvider(IOptions<MockScannerOptions> opts) => _opts = opts.Value;

    public Task<ScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var (status, threats) = _opts.MockResult.ToLowerInvariant() switch
        {
            "infected" => (ScanStatus.Infected, new List<string> { "Eicar-Test-Signature" }),
            "failed"   => (ScanStatus.Failed,   new List<string>()),
            _          => (ScanStatus.Clean,     new List<string>()),
        };

        return Task.FromResult(new ScanResult
        {
            Status    = status,
            Threats   = threats,
            DurationMs = 5,
        });
    }
}
