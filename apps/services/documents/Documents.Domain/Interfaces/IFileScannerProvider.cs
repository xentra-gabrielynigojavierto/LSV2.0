using Documents.Domain.Enums;

namespace Documents.Domain.Interfaces;

public sealed class ScanResult
{
    public ScanStatus   Status          { get; init; }
    public List<string> Threats         { get; init; } = new();
    public int          DurationMs      { get; init; }
    public string?      EngineVersion   { get; init; }
}

public interface IFileScannerProvider
{
    string ProviderName { get; }
    Task<ScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct = default);
}
