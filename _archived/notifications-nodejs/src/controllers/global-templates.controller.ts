import { Request, Response, NextFunction } from "express";
import { TemplateRepository, TemplateVersionRepository } from "../repositories/template.repository";
import { templateRenderingService } from "../services/template-rendering.service";
import { brandingResolutionService } from "../services/branding-resolution.service";
import { auditClient } from "../integrations/audit/audit.client";
import {
  NotificationChannel,
  NotificationChannels,
  ProductType,
  ProductTypes,
  TemplateScope,
  TemplateScopes,
  EditorType,
  EditorTypes,
} from "../types";

const templateRepo = new TemplateRepository();
const versionRepo = new TemplateVersionRepository();

function isString(v: unknown): v is string {
  return typeof v === "string";
}

function isValidJson(v: unknown): boolean {
  if (typeof v === "object" && v !== null) return true;
  if (typeof v === "string") {
    try { JSON.parse(v); return true; } catch { return false; }
  }
  return false;
}

function toJsonString(v: unknown): string {
  if (typeof v === "string") return v;
  return JSON.stringify(v);
}

export const globalTemplatesController = {
  async list(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const query = req.query as Record<string, string | undefined>;
      const limit = parseInt(query["limit"] ?? "20", 10);
      const offset = parseInt(query["offset"] ?? "0", 10);
      const result = await templateRepo.listGlobal({
        productType: query["productType"] as ProductType | undefined,
        channel: query["channel"] as NotificationChannel | undefined,
        templateKey: query["templateKey"],
        status: query["status"] as "active" | "inactive" | undefined,
        limit: isNaN(limit) ? 20 : limit,
        offset: isNaN(offset) ? 0 : offset,
      });
      res.status(200).json({ data: result.rows, meta: { total: result.count, limit, offset } });
    } catch (err) { next(err); }
  },

  async get(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const { id } = req.params as { id: string };
      const template = await templateRepo.findById(id);
      if (!template || template.templateScope !== "global") {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Global template ${id} not found` } });
        return;
      }
      res.status(200).json({ data: template });
    } catch (err) { next(err); }
  },

  async create(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const body = req.body as Record<string, unknown>;
      const errors: string[] = [];

      if (!body["templateKey"] || !isString(body["templateKey"])) errors.push("templateKey is required");
      if (!body["channel"] || !NotificationChannels.includes(body["channel"] as NotificationChannel)) {
        errors.push(`channel must be one of: ${NotificationChannels.join(", ")}`);
      }
      if (!body["name"] || !isString(body["name"])) errors.push("name is required");
      if (!body["productType"] || !ProductTypes.includes(body["productType"] as ProductType)) {
        errors.push(`productType must be one of: ${ProductTypes.join(", ")}`);
      }
      if (body["editorType"] && !EditorTypes.includes(body["editorType"] as EditorType)) {
        errors.push(`editorType must be one of: ${EditorTypes.join(", ")}`);
      }

      if (errors.length > 0) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: "Invalid request", details: errors } });
        return;
      }

      const productType = body["productType"] as ProductType;
      const channel = body["channel"] as NotificationChannel;
      const templateKey = body["templateKey"] as string;
      const editorType = (body["editorType"] as EditorType) ?? "html";

      const existing = await templateRepo.findGlobalByProductKey(productType, channel, templateKey, "global");
      if (existing) {
        res.status(409).json({
          error: {
            code: "CONFLICT",
            message: `Global template '${templateKey}' already exists for product '${productType}' channel '${channel}'`,
          },
        });
        return;
      }

      const template = await templateRepo.create({
        tenantId: null,
        templateKey,
        channel,
        name: body["name"] as string,
        description: isString(body["description"]) ? body["description"] : null,
        isSystemTemplate: true,
        productType,
        templateScope: "global",
        editorType,
        category: isString(body["category"]) ? body["category"] : null,
        isBrandable: body["isBrandable"] === true,
      });

      await auditClient.publishEvent({
        eventType: "global_template.created",
        metadata: { templateId: template.id, templateKey, channel, productType },
      });

      res.status(201).json({ data: template });
    } catch (err) { next(err); }
  },

  async update(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const { id } = req.params as { id: string };
      const body = req.body as Record<string, unknown>;
      const template = await templateRepo.findById(id);
      if (!template || template.templateScope !== "global") {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Global template ${id} not found` } });
        return;
      }

      const updates: {
        name?: string;
        description?: string | null;
        status?: "active" | "inactive";
        category?: string | null;
        isBrandable?: boolean;
        editorType?: EditorType;
      } = {};

      if (isString(body["name"])) updates.name = body["name"];
      if (body["description"] !== undefined) updates.description = isString(body["description"]) ? body["description"] : null;
      if (body["status"] === "active" || body["status"] === "inactive") updates.status = body["status"];
      if (body["category"] !== undefined) updates.category = isString(body["category"]) ? body["category"] : null;
      if (typeof body["isBrandable"] === "boolean") updates.isBrandable = body["isBrandable"];
      if (body["editorType"] && EditorTypes.includes(body["editorType"] as EditorType)) {
        updates.editorType = body["editorType"] as EditorType;
      }

      await templateRepo.update(id, updates);
      await auditClient.publishEvent({
        eventType: "global_template.updated",
        metadata: { templateId: id, updates },
      });
      const updated = await templateRepo.findById(id);
      res.status(200).json({ data: updated });
    } catch (err) { next(err); }
  },

  async listVersions(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const { id } = req.params as { id: string };
      const template = await templateRepo.findById(id);
      if (!template || template.templateScope !== "global") {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Global template ${id} not found` } });
        return;
      }
      const versions = await versionRepo.findByTemplateId(id);
      res.status(200).json({ data: versions });
    } catch (err) { next(err); }
  },

  async getVersion(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const { id, versionId } = req.params as { id: string; versionId: string };
      const template = await templateRepo.findById(id);
      if (!template || template.templateScope !== "global") {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Global template ${id} not found` } });
        return;
      }
      const version = await versionRepo.findById(versionId);
      if (!version || version.templateId !== id) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Version ${versionId} not found` } });
        return;
      }
      res.status(200).json({ data: version });
    } catch (err) { next(err); }
  },

  async createVersion(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const { id } = req.params as { id: string };
      const body = req.body as Record<string, unknown>;
      const errors: string[] = [];

      const template = await templateRepo.findById(id);
      if (!template || template.templateScope !== "global") {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Global template ${id} not found` } });
        return;
      }

      if (!body["bodyTemplate"] || !isString(body["bodyTemplate"])) errors.push("bodyTemplate is required");
      if (template.channel === "email") {
        if (!body["subjectTemplate"] || !isString(body["subjectTemplate"])) {
          errors.push("subjectTemplate is required for email templates");
        }
      }

      if (template.editorType === "wysiwyg") {
        if (!body["editorJson"]) {
          errors.push("editorJson is required for WYSIWYG templates");
        } else if (!isValidJson(body["editorJson"])) {
          errors.push("editorJson must be valid JSON");
        }
      }

      if (body["designTokensJson"] && !isValidJson(body["designTokensJson"])) {
        errors.push("designTokensJson must be valid JSON if provided");
      }

      if (errors.length > 0) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: "Invalid request", details: errors } });
        return;
      }

      const version = await versionRepo.create({
        templateId: id,
        subjectTemplate: isString(body["subjectTemplate"]) ? body["subjectTemplate"] : null,
        bodyTemplate: body["bodyTemplate"] as string,
        textTemplate: isString(body["textTemplate"]) ? body["textTemplate"] : null,
        variablesSchemaJson: body["variablesSchemaJson"]
          ? (isString(body["variablesSchemaJson"]) ? body["variablesSchemaJson"] : JSON.stringify(body["variablesSchemaJson"]))
          : null,
        sampleDataJson: body["sampleDataJson"]
          ? (isString(body["sampleDataJson"]) ? body["sampleDataJson"] : JSON.stringify(body["sampleDataJson"]))
          : null,
        editorJson: body["editorJson"] ? toJsonString(body["editorJson"]) : null,
        designTokensJson: body["designTokensJson"] ? toJsonString(body["designTokensJson"]) : null,
        layoutType: isString(body["layoutType"]) ? body["layoutType"] : null,
      });

      await auditClient.publishEvent({
        eventType: "global_template.version.created",
        metadata: {
          templateId: id,
          versionId: version.id,
          versionNumber: version.versionNumber,
          productType: template.productType,
        },
      });

      res.status(201).json({ data: version });
    } catch (err) { next(err); }
  },

  async publishVersion(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const { id, versionId } = req.params as { id: string; versionId: string };
      const template = await templateRepo.findById(id);
      if (!template || template.templateScope !== "global") {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Global template ${id} not found` } });
        return;
      }
      const version = await versionRepo.findById(versionId);
      if (!version || version.templateId !== id) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Version ${versionId} not found` } });
        return;
      }
      if (version.status === "published") {
        res.status(409).json({ error: { code: "ALREADY_PUBLISHED", message: "This version is already published" } });
        return;
      }
      if (version.status === "retired") {
        res.status(409).json({ error: { code: "RETIRED", message: "Cannot publish a retired version" } });
        return;
      }

      await versionRepo.publish(id, versionId);

      await auditClient.publishEvent({
        eventType: "global_template.version.published",
        metadata: {
          templateId: id,
          versionId,
          versionNumber: version.versionNumber,
          productType: template.productType,
        },
      });

      res.status(200).json({ data: { templateId: id, versionId, status: "published" } });
    } catch (err) { next(err); }
  },

  async brandedPreview(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const { id, versionId } = req.params as { id: string; versionId: string };
      const body = req.body as Record<string, unknown>;
      const errors: string[] = [];

      if (!body["tenantId"] || !isString(body["tenantId"])) errors.push("tenantId is required");
      if (!body["productType"] || !ProductTypes.includes(body["productType"] as ProductType)) {
        errors.push(`productType must be one of: ${ProductTypes.join(", ")}`);
      }

      if (errors.length > 0) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: "Invalid request", details: errors } });
        return;
      }

      const template = await templateRepo.findById(id);
      if (!template || template.templateScope !== "global") {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Global template ${id} not found` } });
        return;
      }

      const version = await versionRepo.findById(versionId);
      if (!version || version.templateId !== id) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Version ${versionId} not found` } });
        return;
      }

      const tenantId = body["tenantId"] as string;
      const productType = body["productType"] as ProductType;
      const templateData = (body["templateData"] ?? {}) as Record<string, unknown>;

      const branding = await brandingResolutionService.resolve(tenantId, productType);
      const brandingTokens = brandingResolutionService.buildBrandingTokens(branding);

      const { result, errors: renderErrors } = templateRenderingService.renderBranded(
        version,
        templateData,
        brandingTokens
      );

      if (renderErrors.length > 0) {
        res.status(422).json({ error: { code: "RENDER_ERROR", message: "Rendering failed", details: renderErrors } });
        return;
      }

      await auditClient.publishEvent({
        eventType: "global_template.preview.branded",
        tenantId,
        metadata: {
          templateId: id,
          versionId,
          productType,
          brandingSource: branding.source,
        },
      });

      res.status(200).json({
        data: {
          templateId: id,
          versionId,
          ...result,
          branding: {
            source: branding.source,
            name: branding.name,
            primaryColor: branding.primaryColor,
          },
        },
      });
    } catch (err) { next(err); }
  },
};
