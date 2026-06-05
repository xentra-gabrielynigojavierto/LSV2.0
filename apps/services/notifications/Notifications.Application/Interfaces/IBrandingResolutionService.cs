namespace Notifications.Application.Interfaces;

public class ResolvedBranding
{
    public string Name { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = string.Empty;
    public string SecondaryColor { get; set; } = string.Empty;
    public string AccentColor { get; set; } = string.Empty;
    public string TextColor { get; set; } = string.Empty;
    public string BackgroundColor { get; set; } = string.Empty;
    public string ButtonRadius { get; set; } = string.Empty;
    public string FontFamily { get; set; } = string.Empty;
    public string SupportEmail { get; set; } = string.Empty;
    public string SupportPhone { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string EmailHeaderHtml { get; set; } = string.Empty;
    public string EmailFooterHtml { get; set; } = string.Empty;
    public string Source { get; set; } = "default";
}

public interface IBrandingResolutionService
{
    Task<ResolvedBranding> ResolveAsync(Guid tenantId, string productType);
    ResolvedBranding GetDefault(string productType);
    Dictionary<string, string> BuildBrandingTokens(ResolvedBranding branding);
}
