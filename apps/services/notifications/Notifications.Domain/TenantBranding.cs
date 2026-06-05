namespace Notifications.Domain;

public class TenantBranding
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ProductType { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string? TextColor { get; set; }
    public string? BackgroundColor { get; set; }
    public string? ButtonRadius { get; set; }
    public string? FontFamily { get; set; }
    public string? SupportEmail { get; set; }
    public string? SupportPhone { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? EmailHeaderHtml { get; set; }
    public string? EmailFooterHtml { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
