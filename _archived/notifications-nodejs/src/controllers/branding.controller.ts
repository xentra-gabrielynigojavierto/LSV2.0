import { Request, Response, NextFunction } from "express";
import { TenantBrandingRepository } from "../repositories/tenant-branding.repository";
import { auditClient } from "../integrations/audit/audit.client";
import { ProductType, ProductTypes } from "../types";

const brandingRepo = new TenantBrandingRepository();

function isString(v: unknown): v is string {
  return typeof v === "string";
}

const NULLABLE_STRING_FIELDS = [
  "logoUrl",
  "primaryColor",
  "secondaryColor",
  "accentColor",
  "textColor",
  "backgroundColor",
  "buttonRadius",
  "fontFamily",
  "emailHeaderHtml",
  "emailFooterHtml",
  "supportEmail",
  "supportPhone",
  "websiteUrl",
] as const;

export const brandingController = {
  async list(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const query = req.query as Record<string, string | undefined>;
      const limit = parseInt(query["limit"] ?? "20", 10);
      const offset = parseInt(query["offset"] ?? "0", 10);
      const result = await brandingRepo.list({
        tenantId,
        productType: query["productType"] as ProductType | undefined,
        limit: isNaN(limit) ? 20 : limit,
        offset: isNaN(offset) ? 0 : offset,
      });
      res.status(200).json({ data: result.rows, meta: { total: result.count, limit, offset } });
    } catch (err) { next(err); }
  },

  async get(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { id } = req.params as { id: string };
      const branding = await brandingRepo.findById(id);
      if (!branding || branding.tenantId !== tenantId) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Branding ${id} not found` } });
        return;
      }
      res.status(200).json({ data: branding });
    } catch (err) { next(err); }
  },

  async create(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const body = req.body as Record<string, unknown>;
      const errors: string[] = [];

      if (!body["productType"] || !ProductTypes.includes(body["productType"] as ProductType)) {
        errors.push(`productType must be one of: ${ProductTypes.join(", ")}`);
      }
      if (!body["brandName"] || !isString(body["brandName"])) errors.push("brandName is required");

      if (errors.length > 0) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: "Invalid request", details: errors } });
        return;
      }

      const productType = body["productType"] as ProductType;

      const existing = await brandingRepo.findByTenantAndProduct(tenantId, productType);
      if (existing) {
        res.status(409).json({
          error: {
            code: "CONFLICT",
            message: `Branding already exists for tenant '${tenantId}' product '${productType}'`,
          },
        });
        return;
      }

      const input: Record<string, unknown> = {
        tenantId,
        productType,
        brandName: body["brandName"] as string,
      };

      for (const field of NULLABLE_STRING_FIELDS) {
        if (isString(body[field])) {
          input[field] = body[field];
        }
      }

      const branding = await brandingRepo.create(input as Parameters<typeof brandingRepo.create>[0]);

      await auditClient.publishEvent({
        eventType: "tenant_branding.created",
        tenantId,
        metadata: { brandingId: branding.id, productType },
      });

      res.status(201).json({ data: branding });
    } catch (err) { next(err); }
  },

  async update(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { id } = req.params as { id: string };
      const body = req.body as Record<string, unknown>;

      const branding = await brandingRepo.findById(id);
      if (!branding || branding.tenantId !== tenantId) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Branding ${id} not found` } });
        return;
      }

      const updates: Record<string, unknown> = {};
      if (isString(body["brandName"])) updates["brandName"] = body["brandName"];

      for (const field of NULLABLE_STRING_FIELDS) {
        if (body[field] !== undefined) {
          updates[field] = isString(body[field]) ? body[field] : null;
        }
      }

      await brandingRepo.update(id, updates as Parameters<typeof brandingRepo.update>[1]);

      await auditClient.publishEvent({
        eventType: "tenant_branding.updated",
        tenantId: branding.tenantId,
        metadata: { brandingId: id, updates: Object.keys(updates) },
      });

      const updated = await brandingRepo.findById(id);
      res.status(200).json({ data: updated });
    } catch (err) { next(err); }
  },
};
