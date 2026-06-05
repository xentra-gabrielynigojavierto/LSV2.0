using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

public class BrandingResolutionService : IBrandingResolutionService
{
    private readonly ITenantBrandingRepository _brandingRepo;
    private readonly ILogger<BrandingResolutionService> _logger;

    private static readonly Dictionary<string, ResolvedBranding> ProductDefaults = new()
    {
        ["careconnect"] = new ResolvedBranding
        {
            Name = "CareConnect", PrimaryColor = "#2563EB", SecondaryColor = "#1E40AF", AccentColor = "#3B82F6",
            TextColor = "#1F2937", BackgroundColor = "#FFFFFF", ButtonRadius = "6px",
            FontFamily = "Inter, system-ui, sans-serif", SupportEmail = "support@careconnect.com",
            WebsiteUrl = "https://careconnect.com", Source = "default"
        }
    };

    private static readonly ResolvedBranding PlatformDefault = new()
    {
        Name = "LegalSynq", PrimaryColor = "#4F46E5", SecondaryColor = "#4338CA", AccentColor = "#6366F1",
        TextColor = "#1F2937", BackgroundColor = "#FFFFFF", ButtonRadius = "6px",
        FontFamily = "Inter, system-ui, sans-serif", SupportEmail = "support@legalsynq.com",
        WebsiteUrl = "https://legalsynq.com", Source = "default"
    };

    public BrandingResolutionService(ITenantBrandingRepository brandingRepo, ILogger<BrandingResolutionService> logger)
    {
        _brandingRepo = brandingRepo;
        _logger = logger;
    }

    public async Task<ResolvedBranding> ResolveAsync(Guid tenantId, string productType)
    {
        var tenantBranding = await _brandingRepo.FindByTenantAndProductAsync(tenantId, productType);
        if (tenantBranding != null)
        {
            _logger.LogDebug("Branding resolved from tenant record: {TenantId} {ProductType}", tenantId, productType);
            var defaults = GetDefault(productType);
            return new ResolvedBranding
            {
                Name = tenantBranding.BrandName,
                LogoUrl = tenantBranding.LogoUrl ?? defaults.LogoUrl,
                PrimaryColor = tenantBranding.PrimaryColor ?? defaults.PrimaryColor,
                SecondaryColor = tenantBranding.SecondaryColor ?? defaults.SecondaryColor,
                AccentColor = tenantBranding.AccentColor ?? defaults.AccentColor,
                TextColor = tenantBranding.TextColor ?? defaults.TextColor,
                BackgroundColor = tenantBranding.BackgroundColor ?? defaults.BackgroundColor,
                ButtonRadius = tenantBranding.ButtonRadius ?? defaults.ButtonRadius,
                FontFamily = tenantBranding.FontFamily ?? defaults.FontFamily,
                SupportEmail = tenantBranding.SupportEmail ?? defaults.SupportEmail,
                SupportPhone = tenantBranding.SupportPhone ?? defaults.SupportPhone,
                WebsiteUrl = tenantBranding.WebsiteUrl ?? defaults.WebsiteUrl,
                EmailHeaderHtml = tenantBranding.EmailHeaderHtml ?? defaults.EmailHeaderHtml,
                EmailFooterHtml = tenantBranding.EmailFooterHtml ?? defaults.EmailFooterHtml,
                Source = "tenant"
            };
        }

        _logger.LogDebug("Branding resolved from product defaults: {TenantId} {ProductType}", tenantId, productType);
        return GetDefault(productType);
    }

    public ResolvedBranding GetDefault(string productType)
        => ProductDefaults.TryGetValue(productType, out var def) ? def : new ResolvedBranding
        {
            Name = PlatformDefault.Name, LogoUrl = PlatformDefault.LogoUrl,
            PrimaryColor = PlatformDefault.PrimaryColor, SecondaryColor = PlatformDefault.SecondaryColor,
            AccentColor = PlatformDefault.AccentColor, TextColor = PlatformDefault.TextColor,
            BackgroundColor = PlatformDefault.BackgroundColor, ButtonRadius = PlatformDefault.ButtonRadius,
            FontFamily = PlatformDefault.FontFamily, SupportEmail = PlatformDefault.SupportEmail,
            SupportPhone = PlatformDefault.SupportPhone, WebsiteUrl = PlatformDefault.WebsiteUrl,
            Source = "default"
        };

    public Dictionary<string, string> BuildBrandingTokens(ResolvedBranding branding) => new()
    {
        ["brand.name"] = branding.Name, ["brand.logoUrl"] = branding.LogoUrl,
        ["brand.primaryColor"] = branding.PrimaryColor, ["brand.secondaryColor"] = branding.SecondaryColor,
        ["brand.accentColor"] = branding.AccentColor, ["brand.textColor"] = branding.TextColor,
        ["brand.backgroundColor"] = branding.BackgroundColor, ["brand.buttonRadius"] = branding.ButtonRadius,
        ["brand.fontFamily"] = branding.FontFamily, ["brand.supportEmail"] = branding.SupportEmail,
        ["brand.supportPhone"] = branding.SupportPhone, ["brand.websiteUrl"] = branding.WebsiteUrl
    };
}
