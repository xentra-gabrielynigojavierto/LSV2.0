import { TenantBrandingRepository } from "../repositories/tenant-branding.repository";
import { ProductType } from "../types";
import { logger } from "../shared/logger";

export interface ResolvedBranding {
  name: string;
  logoUrl: string;
  primaryColor: string;
  secondaryColor: string;
  accentColor: string;
  textColor: string;
  backgroundColor: string;
  buttonRadius: string;
  fontFamily: string;
  supportEmail: string;
  supportPhone: string;
  websiteUrl: string;
  emailHeaderHtml: string;
  emailFooterHtml: string;
  source: "tenant" | "default";
}

const PRODUCT_DEFAULTS: Record<string, ResolvedBranding> = {
  careconnect: {
    name: "CareConnect",
    logoUrl: "",
    primaryColor: "#2563EB",
    secondaryColor: "#1E40AF",
    accentColor: "#3B82F6",
    textColor: "#1F2937",
    backgroundColor: "#FFFFFF",
    buttonRadius: "6px",
    fontFamily: "Inter, system-ui, sans-serif",
    supportEmail: "support@careconnect.com",
    supportPhone: "",
    websiteUrl: "https://careconnect.com",
    emailHeaderHtml: "",
    emailFooterHtml: "",
    source: "default",
  },
};

const PLATFORM_DEFAULT: ResolvedBranding = {
  name: "LegalSynq",
  logoUrl: "",
  primaryColor: "#4F46E5",
  secondaryColor: "#4338CA",
  accentColor: "#6366F1",
  textColor: "#1F2937",
  backgroundColor: "#FFFFFF",
  buttonRadius: "6px",
  fontFamily: "Inter, system-ui, sans-serif",
  supportEmail: "support@legalsynq.com",
  supportPhone: "",
  websiteUrl: "https://legalsynq.com",
  emailHeaderHtml: "",
  emailFooterHtml: "",
  source: "default",
};

export class BrandingResolutionService {
  private brandingRepo: TenantBrandingRepository;

  constructor() {
    this.brandingRepo = new TenantBrandingRepository();
  }

  async resolve(tenantId: string, productType: ProductType): Promise<ResolvedBranding> {
    const tenantBranding = await this.brandingRepo.findByTenantAndProduct(tenantId, productType);

    if (tenantBranding) {
      logger.debug("Branding resolved from tenant record", {
        tenantId,
        productType,
        brandingId: tenantBranding.id,
      });

      const defaults = this.getDefault(productType);
      return {
        name: tenantBranding.brandName,
        logoUrl: tenantBranding.logoUrl ?? defaults.logoUrl,
        primaryColor: tenantBranding.primaryColor ?? defaults.primaryColor,
        secondaryColor: tenantBranding.secondaryColor ?? defaults.secondaryColor,
        accentColor: tenantBranding.accentColor ?? defaults.accentColor,
        textColor: tenantBranding.textColor ?? defaults.textColor,
        backgroundColor: tenantBranding.backgroundColor ?? defaults.backgroundColor,
        buttonRadius: tenantBranding.buttonRadius ?? defaults.buttonRadius,
        fontFamily: tenantBranding.fontFamily ?? defaults.fontFamily,
        supportEmail: tenantBranding.supportEmail ?? defaults.supportEmail,
        supportPhone: tenantBranding.supportPhone ?? defaults.supportPhone,
        websiteUrl: tenantBranding.websiteUrl ?? defaults.websiteUrl,
        emailHeaderHtml: tenantBranding.emailHeaderHtml ?? defaults.emailHeaderHtml,
        emailFooterHtml: tenantBranding.emailFooterHtml ?? defaults.emailFooterHtml,
        source: "tenant",
      };
    }

    logger.debug("Branding resolved from product defaults", { tenantId, productType });
    return this.getDefault(productType);
  }

  getDefault(productType: ProductType): ResolvedBranding {
    return PRODUCT_DEFAULTS[productType] ?? { ...PLATFORM_DEFAULT };
  }

  buildBrandingTokens(branding: ResolvedBranding): Record<string, string> {
    return {
      "brand.name": branding.name,
      "brand.logoUrl": branding.logoUrl,
      "brand.primaryColor": branding.primaryColor,
      "brand.secondaryColor": branding.secondaryColor,
      "brand.accentColor": branding.accentColor,
      "brand.textColor": branding.textColor,
      "brand.backgroundColor": branding.backgroundColor,
      "brand.buttonRadius": branding.buttonRadius,
      "brand.fontFamily": branding.fontFamily,
      "brand.supportEmail": branding.supportEmail,
      "brand.supportPhone": branding.supportPhone,
      "brand.websiteUrl": branding.websiteUrl,
    };
  }
}

export const brandingResolutionService = new BrandingResolutionService();
