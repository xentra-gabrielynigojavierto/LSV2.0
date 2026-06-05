namespace Notifications.Domain;

public class TenantContactPolicy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? Channel { get; set; }
    public bool BlockSuppressedContacts { get; set; } = true;
    public bool BlockUnsubscribedContacts { get; set; } = true;
    public bool BlockComplainedContacts { get; set; } = true;
    public bool BlockBouncedContacts { get; set; }
    public bool BlockInvalidContacts { get; set; }
    public bool BlockCarrierRejectedContacts { get; set; }
    public bool AllowManualOverride { get; set; }

    /// <summary>
    /// When true (default), SMS sends are blocked if the recipient's SMS preference state is unknown.
    /// Conservative opt-in default: tenants must explicitly set phone preferences to opted_in,
    /// OR set this to false to allow sending to numbers with no recorded preference.
    /// </summary>
    public bool BlockUnknownSmsPreference { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
