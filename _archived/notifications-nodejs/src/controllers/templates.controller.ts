import { Request, Response, NextFunction } from "express";
import { TemplateRepository, TemplateVersionRepository } from "../repositories/template.repository";
import { templateRenderingService } from "../services/template-rendering.service";
import { auditClient } from "../integrations/audit/audit.client";
import { NotificationChannel, NotificationChannels } from "../types";

const templateRepo = new TemplateRepository();
const versionRepo = new TemplateVersionRepository();

function isString(v: unknown): v is string {
  return typeof v === "string";
}

export const templatesController = {
  async list(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const query = req.query as Record<string, string | undefined>;
      const channel = query["channel"] as NotificationChannel | undefined;
      const status = query["status"] as "active" | "inactive" | undefined;
      const limit = parseInt(query["limit"] ?? "20", 10);
      const offset = parseInt(query["offset"] ?? "0", 10);
      const result = await templateRepo.list({
        tenantId: req.tenantId || undefined,
        channel,
        status,
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
      if (!template) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Template ${id} not found` } });
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
      if (errors.length > 0) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: "Invalid request", details: errors } });
        return;
      }
      const channel = body["channel"] as NotificationChannel;
      const templateKey = body["templateKey"] as string;
      const existing = await templateRepo.findByKey(templateKey, channel, req.tenantId ?? null);
      if (existing) {
        res.status(409).json({ error: { code: "CONFLICT", message: `Template key '${templateKey}' already exists for channel '${channel}'` } });
        return;
      }
      const template = await templateRepo.create({
        tenantId: req.tenantId ?? null,
        templateKey,
        channel,
        name: body["name"] as string,
        description: isString(body["description"]) ? body["description"] : null,
        isSystemTemplate: body["isSystemTemplate"] === true,
      });
      await auditClient.publishEvent({
        eventType: "template.created",
        tenantId: req.tenantId,
        metadata: { templateId: template.id, templateKey, channel },
      });
      res.status(201).json({ data: template });
    } catch (err) { next(err); }
  },

  async update(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const { id } = req.params as { id: string };
      const body = req.body as Record<string, unknown>;
      const template = await templateRepo.findById(id);
      if (!template) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Template ${id} not found` } });
        return;
      }
      const updates: { name?: string; description?: string | null; status?: "active" | "inactive" } = {};
      if (isString(body["name"])) updates.name = body["name"];
      if (body["description"] !== undefined) updates.description = isString(body["description"]) ? body["description"] : null;
      if (body["status"] === "active" || body["status"] === "inactive") updates.status = body["status"];
      await templateRepo.update(id, updates);
      await auditClient.publishEvent({
        eventType: "template.updated",
        tenantId: req.tenantId,
        metadata: { templateId: id, updates },
      });
      res.status(200).json({ data: { id, ...updates } });
    } catch (err) { next(err); }
  },

  remove(_req: Request, res: Response): void {
    res.status(405).json({ error: { code: "METHOD_NOT_ALLOWED", message: "Templates cannot be deleted. Set status to inactive instead." } });
  },

  async listVersions(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const { id } = req.params as { id: string };
      const template = await templateRepo.findById(id);
      if (!template) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Template ${id} not found` } });
        return;
      }
      const versions = await versionRepo.findByTemplateId(id);
      res.status(200).json({ data: versions });
    } catch (err) { next(err); }
  },

  async getVersion(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const { id, versionId } = req.params as { id: string; versionId: string };
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
      if (!template) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Template ${id} not found` } });
        return;
      }
      if (!body["bodyTemplate"] || !isString(body["bodyTemplate"])) errors.push("bodyTemplate is required");
      if (template.channel === "email") {
        if (!body["subjectTemplate"] || !isString(body["subjectTemplate"])) {
          errors.push("subjectTemplate is required for email templates");
        }
      }
      if (errors.length > 0) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: "Invalid request", details: errors } });
        return;
      }
      const variablesSchemaRaw = body["variablesSchemaJson"];
      const sampleDataRaw = body["sampleDataJson"];
      const version = await versionRepo.create({
        templateId: id,
        subjectTemplate: isString(body["subjectTemplate"]) ? body["subjectTemplate"] : null,
        bodyTemplate: body["bodyTemplate"] as string,
        textTemplate: isString(body["textTemplate"]) ? body["textTemplate"] : null,
        variablesSchemaJson: variablesSchemaRaw ? (isString(variablesSchemaRaw) ? variablesSchemaRaw : JSON.stringify(variablesSchemaRaw)) : null,
        sampleDataJson: sampleDataRaw ? (isString(sampleDataRaw) ? sampleDataRaw : JSON.stringify(sampleDataRaw)) : null,
      });
      await auditClient.publishEvent({
        eventType: "template.version.created",
        tenantId: req.tenantId,
        metadata: { templateId: id, versionId: version.id, versionNumber: version.versionNumber },
      });
      res.status(201).json({ data: version });
    } catch (err) { next(err); }
  },

  async publishVersion(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const { id, versionId } = req.params as { id: string; versionId: string };
      const template = await templateRepo.findById(id);
      if (!template) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Template ${id} not found` } });
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
        eventType: "template.version.published",
        tenantId: req.tenantId,
        metadata: { templateId: id, versionId, versionNumber: version.versionNumber },
      });
      res.status(200).json({ data: { templateId: id, versionId, status: "published" } });
    } catch (err) { next(err); }
  },

  async previewLatest(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const { id } = req.params as { id: string };
      const body = req.body as Record<string, unknown>;
      const template = await templateRepo.findById(id);
      if (!template) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Template ${id} not found` } });
        return;
      }
      const version = await versionRepo.findPublishedByTemplateId(id);
      if (!version) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `No published version found for template ${id}` } });
        return;
      }
      const templateData = (body["templateData"] ?? {}) as Record<string, unknown>;
      const { result, errors } = templateRenderingService.render(version, templateData);
      if (errors.length > 0) {
        res.status(422).json({ error: { code: "RENDER_ERROR", message: "Rendering failed", details: errors } });
        return;
      }
      await auditClient.publishEvent({
        eventType: "template.preview.rendered",
        tenantId: req.tenantId,
        metadata: { templateId: id, versionId: version.id },
      });
      res.status(200).json({ data: { templateId: id, versionId: version.id, ...result } });
    } catch (err) { next(err); }
  },

  async previewVersion(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const { id, versionId } = req.params as { id: string; versionId: string };
      const body = req.body as Record<string, unknown>;
      const template = await templateRepo.findById(id);
      if (!template) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Template ${id} not found` } });
        return;
      }
      const version = await versionRepo.findById(versionId);
      if (!version || version.templateId !== id) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `Version ${versionId} not found` } });
        return;
      }
      const templateData = (body["templateData"] ?? {}) as Record<string, unknown>;
      const { result, errors } = templateRenderingService.render(version, templateData);
      if (errors.length > 0) {
        res.status(422).json({ error: { code: "RENDER_ERROR", message: "Rendering failed", details: errors } });
        return;
      }
      await auditClient.publishEvent({
        eventType: "template.preview.rendered",
        tenantId: req.tenantId,
        metadata: { templateId: id, versionId, versionStatus: version.status },
      });
      res.status(200).json({ data: { templateId: id, versionId, ...result } });
    } catch (err) { next(err); }
  },

  async getByKey(req: Request, res: Response, next: NextFunction): Promise<void> {
    try {
      const { templateKey } = req.params as { templateKey: string };
      const channel = req.query["channel"] as NotificationChannel | undefined;
      if (!channel || !NotificationChannels.includes(channel)) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: `channel query param must be one of: ${NotificationChannels.join(", ")}` } });
        return;
      }
      const tenantTemplate = await templateRepo.findByKey(templateKey, channel, req.tenantId ?? null);
      const globalTemplate = await templateRepo.findByKey(templateKey, channel, null);
      const resolved = tenantTemplate ?? globalTemplate;
      if (!resolved) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: `No template found with key '${templateKey}' for channel '${channel}'` } });
        return;
      }
      const publishedVersion = await versionRepo.findPublishedByTemplateId(resolved.id);
      res.status(200).json({ data: { template: resolved, publishedVersion: publishedVersion ?? null } });
    } catch (err) { next(err); }
  },
};
