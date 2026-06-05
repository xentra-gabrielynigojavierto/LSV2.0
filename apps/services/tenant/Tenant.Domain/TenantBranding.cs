namespace Tenant.Domain;

/// <summary>
/// One-to-one branding metadata record owned by the Tenant service.
///
/// Stores document references (not blobs) and theme colours.
/// The Documents service owns binary storage; this entity tracks the IDs.
/// </summary>
public class TenantBranding
{
    public Guid Id { get; private set; }

    /// <summary>Owning tenant. One-to-one FK on tenant_Tenants.Id.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Optional brand name override if different from Tenant.DisplayName.</summary>
    public string? BrandName { get; private set; }

    // ── Asset references ──────────────────────────────────────────────────────

    /// <summary>Primary logo document ref (Documents service). Not a blob.</summary>
    public Guid? LogoDocumentId { get; private set; }

    /// <summary>White / reverse logo variant ref.</summary>
    public Guid? LogoWhiteDocumentId { get; private set; }

    /// <summary>Favicon document ref. Nullable — scaffold now, use later.</summary>
    public Guid? FaviconDocumentId { get; private set; }

    // ── Theme colours (hex, e.g. #1A2B3C) ────────────────────────────────────

    public string? PrimaryColor    { get; private set; }
    public string? SecondaryColor  { get; private set; }
    public string? AccentColor     { get; private set; }
    public string? TextColor       { get; private set; }
    public string? BackgroundColor { get; private set; }

    // ── Contact / web overrides ───────────────────────────────────────────────

    /// <summary>Brand-specific website URL override (nullable = use Tenant.WebsiteUrl).</summary>
    public string? WebsiteUrlOverride    { get; private set; }

    /// <summary>Brand-specific support email override.</summary>
    public string? SupportEmailOverride  { get; private set; }

    /// <summary>Brand-specific support phone override.</summary>
    public string? SupportPhoneOverride  { get; private set; }

    // ── Timestamps ────────────────────────────────────────────────────────────

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    public Tenant? Tenant { get; private set; }

    private TenantBranding() { }

    // ── Factory ───────────────────────────────────────────────────────────────

    public static TenantBranding Create(Guid tenantId) =>
        new()
        {
            Id           = Guid.NewGuid(),
            TenantId     = tenantId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    // ── Targeted logo mutators ────────────────────────────────────────────────

    /// <summary>Sets only the primary logo reference without touching any other field.</summary>
    public void SetLogo(Guid? documentId)
    {
        LogoDocumentId = documentId;
        UpdatedAtUtc   = DateTime.UtcNow;
    }

    /// <summary>Sets only the white/reversed logo reference without touching any other field.</summary>
    public void SetLogoWhite(Guid? documentId)
    {
        LogoWhiteDocumentId = documentId;
        UpdatedAtUtc        = DateTime.UtcNow;
    }

    // ── Full-replace mutator ──────────────────────────────────────────────────

    public void Update(
        string? brandName           = null,
        Guid?   logoDocumentId      = null,
        Guid?   logoWhiteDocumentId = null,
        Guid?   faviconDocumentId   = null,
        string? primaryColor        = null,
        string? secondaryColor      = null,
        string? accentColor         = null,
        string? textColor           = null,
        string? backgroundColor     = null,
        string? websiteUrlOverride   = null,
        string? supportEmailOverride = null,
        string? supportPhoneOverride = null)
    {
        BrandName            = brandName?.Trim();
        LogoDocumentId       = logoDocumentId;
        LogoWhiteDocumentId  = logoWhiteDocumentId;
        FaviconDocumentId    = faviconDocumentId;
        PrimaryColor         = primaryColor?.Trim().ToUpperInvariant();
        SecondaryColor       = secondaryColor?.Trim().ToUpperInvariant();
        AccentColor          = accentColor?.Trim().ToUpperInvariant();
        TextColor            = textColor?.Trim().ToUpperInvariant();
        BackgroundColor      = backgroundColor?.Trim().ToUpperInvariant();
        WebsiteUrlOverride   = websiteUrlOverride?.Trim();
        SupportEmailOverride = supportEmailOverride?.Trim().ToLowerInvariant();
        SupportPhoneOverride = supportPhoneOverride?.Trim();
        UpdatedAtUtc         = DateTime.UtcNow;
    }
}
