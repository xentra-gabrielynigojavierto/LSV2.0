namespace Notifications.Application.DTOs;

public class ContactSuppressionDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string ContactValue { get; set; } = string.Empty;
    public string SuppressionType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Source { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateContactSuppressionDto
{
    public string Channel { get; set; } = string.Empty;
    public string ContactValue { get; set; } = string.Empty;
    public string SuppressionType { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Notes { get; set; }
}

public class BrandingDto
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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpsertBrandingDto
{
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
}
