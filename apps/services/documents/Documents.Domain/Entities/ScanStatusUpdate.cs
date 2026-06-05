using Documents.Domain.Enums;

namespace Documents.Domain.Entities;

public sealed class ScanStatusUpdate
{
    public ScanStatus   ScanStatus        { get; init; }
    public DateTime?    ScanCompletedAt   { get; init; }
    public int?         ScanDurationMs    { get; init; }
    public List<string> ScanThreats       { get; init; } = new();
    public string?      ScanEngineVersion { get; init; }
}
