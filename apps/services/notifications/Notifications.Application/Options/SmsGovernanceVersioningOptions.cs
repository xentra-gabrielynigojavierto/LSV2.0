namespace Notifications.Application.Options;

/// <summary>LS-NOTIF-SMS-020: Configuration for governance rule versioning.</summary>
public sealed class SmsGovernanceVersioningOptions
{
    public const string SectionName = "SmsGovernanceVersioning";

    /// <summary>Master switch. When false, no snapshots are created.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true, pack snapshots include a JSON array of all current rules.
    /// May increase snapshot size for large packs.
    /// </summary>
    public bool IncludeRulesInPackSnapshot { get; set; } = true;

    /// <summary>Maximum bytes allowed for a single snapshot JSON field (default 64 KB).</summary>
    public int MaxSnapshotJsonBytes { get; set; } = 65536;
}
