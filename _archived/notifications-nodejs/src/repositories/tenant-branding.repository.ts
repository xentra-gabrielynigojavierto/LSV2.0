import { TenantBranding } from "../models/tenant-branding.model";
import { ProductType } from "../types";

export class TenantBrandingRepository {
  async findById(id: string): Promise<TenantBranding | null> {
    return TenantBranding.findByPk(id);
  }

  async findByTenantAndProduct(
    tenantId: string,
    productType: ProductType
  ): Promise<TenantBranding | null> {
    return TenantBranding.findOne({ where: { tenantId, productType } });
  }

  async create(input: {
    tenantId: string;
    productType: ProductType;
    brandName: string;
    logoUrl?: string | null;
    primaryColor?: string | null;
    secondaryColor?: string | null;
    accentColor?: string | null;
    textColor?: string | null;
    backgroundColor?: string | null;
    buttonRadius?: string | null;
    fontFamily?: string | null;
    emailHeaderHtml?: string | null;
    emailFooterHtml?: string | null;
    supportEmail?: string | null;
    supportPhone?: string | null;
    websiteUrl?: string | null;
  }): Promise<TenantBranding> {
    return TenantBranding.create({
      tenantId: input.tenantId,
      productType: input.productType,
      brandName: input.brandName,
      logoUrl: input.logoUrl ?? null,
      primaryColor: input.primaryColor ?? null,
      secondaryColor: input.secondaryColor ?? null,
      accentColor: input.accentColor ?? null,
      textColor: input.textColor ?? null,
      backgroundColor: input.backgroundColor ?? null,
      buttonRadius: input.buttonRadius ?? null,
      fontFamily: input.fontFamily ?? null,
      emailHeaderHtml: input.emailHeaderHtml ?? null,
      emailFooterHtml: input.emailFooterHtml ?? null,
      supportEmail: input.supportEmail ?? null,
      supportPhone: input.supportPhone ?? null,
      websiteUrl: input.websiteUrl ?? null,
    });
  }

  async update(
    id: string,
    input: Partial<{
      brandName: string;
      logoUrl: string | null;
      primaryColor: string | null;
      secondaryColor: string | null;
      accentColor: string | null;
      textColor: string | null;
      backgroundColor: string | null;
      buttonRadius: string | null;
      fontFamily: string | null;
      emailHeaderHtml: string | null;
      emailFooterHtml: string | null;
      supportEmail: string | null;
      supportPhone: string | null;
      websiteUrl: string | null;
    }>
  ): Promise<void> {
    await TenantBranding.update(input, { where: { id } });
  }

  async list(filter: {
    tenantId?: string;
    productType?: ProductType;
    limit?: number;
    offset?: number;
  }): Promise<{ rows: TenantBranding[]; count: number }> {
    const where: Record<string, unknown> = {};
    if (filter.tenantId) where["tenantId"] = filter.tenantId;
    if (filter.productType) where["productType"] = filter.productType;

    const limit = Math.min(filter.limit ?? 20, 100);
    const offset = filter.offset ?? 0;

    return TenantBranding.findAndCountAll({
      where,
      limit,
      offset,
      order: [["createdAt", "DESC"]],
    });
  }
}
