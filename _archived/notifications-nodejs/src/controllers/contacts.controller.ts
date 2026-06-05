import { Request, Response } from "express";
import { ContactSuppressionRepository } from "../repositories/contact-suppression.repository";
import { TenantContactPolicyRepository } from "../repositories/tenant-contact-policy.repository";
import { RecipientContactHealthRepository } from "../repositories/recipient-contact-health.repository";
import { auditClient } from "../integrations/audit/audit.client";
import { logger } from "../shared/logger";
import { normalizeContactValue } from "../shared/contact-normalizer";
import { SuppressionType, SuppressionSource, SuppressionStatus, NotificationChannel } from "../types";

const suppressionRepo = new ContactSuppressionRepository();
const policyRepo = new TenantContactPolicyRepository();
const healthRepo = new RecipientContactHealthRepository();

const VALID_SUPPRESSION_TYPES: SuppressionType[] = [
  "manual", "bounce", "unsubscribe", "complaint", "invalid_contact", "carrier_rejection", "system_protection",
];
const VALID_SUPPRESSION_SOURCES: SuppressionSource[] = [
  "provider_webhook", "manual_admin", "system_rule", "import",
];
const VALID_CHANNELS: NotificationChannel[] = ["email", "sms", "push", "in-app"];

function handleError(res: Response, err: unknown): void {
  const e = err as { statusCode?: number; message?: string; details?: string[] };
  const statusCode = e.statusCode ?? 500;
  logger.error("Contacts controller error", { statusCode, message: e.message });
  res.status(statusCode).json({ error: { code: "CONTACTS_ERROR", message: e.message ?? "Unexpected error", details: e.details } });
}

export const contactsController = {

  // ─── Suppressions ─────────────────────────────────────────────────────────

  async listSuppressions(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { channel, contactValue, status, suppressionType, limit, offset } = req.query as Record<string, string>;
      const result = await suppressionRepo.list({
        tenantId,
        channel,
        contactValue,
        status: status as SuppressionStatus | undefined,
        suppressionType: suppressionType as SuppressionType | undefined,
        limit: limit ? parseInt(limit, 10) : 100,
        offset: offset ? parseInt(offset, 10) : 0,
      });
      res.json({ data: result.rows, count: result.count });
    } catch (err) {
      handleError(res, err);
    }
  },

  async createSuppression(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { channel, contactValue, suppressionType, reason, source, expiresAt, createdBy, notes } =
        req.body as Record<string, unknown>;

      const errors: string[] = [];
      if (!channel || !VALID_CHANNELS.includes(channel as NotificationChannel)) errors.push(`channel must be one of: ${VALID_CHANNELS.join(", ")}`);
      if (!contactValue || typeof contactValue !== "string") errors.push("contactValue is required");
      if (!suppressionType || !VALID_SUPPRESSION_TYPES.includes(suppressionType as SuppressionType)) {
        errors.push(`suppressionType must be one of: ${VALID_SUPPRESSION_TYPES.join(", ")}`);
      }
      if (!reason || typeof reason !== "string") errors.push("reason is required");
      const resolvedSource = (source as SuppressionSource) ?? "manual_admin";
      if (!VALID_SUPPRESSION_SOURCES.includes(resolvedSource)) {
        errors.push(`source must be one of: ${VALID_SUPPRESSION_SOURCES.join(", ")}`);
      }
      if (errors.length > 0) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: "Validation failed", details: errors } });
        return;
      }

      const suppression = await suppressionRepo.create({
        tenantId,
        channel: channel as string,
        contactValue: contactValue as string,
        suppressionType: suppressionType as SuppressionType,
        reason: reason as string,
        source: resolvedSource,
        expiresAt: expiresAt ? new Date(expiresAt as string) : null,
        createdBy: createdBy as string | null ?? null,
        notes: notes as string | null ?? null,
      });

      await auditClient.publishEvent({
        eventType: "contact_suppression.created",
        tenantId,
        channel: channel as string,
        metadata: { suppressionId: suppression.id, suppressionType, contactValue: normalizeContactValue(channel as string, contactValue as string) },
      });

      res.status(201).json({ data: suppression });
    } catch (err) {
      handleError(res, err);
    }
  },

  async getSuppressionById(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { id } = req.params as { id: string };
      const suppression = await suppressionRepo.findByIdAndTenant(id, tenantId);
      if (!suppression) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: "Suppression not found" } });
        return;
      }
      res.json({ data: suppression });
    } catch (err) {
      handleError(res, err);
    }
  },

  async updateSuppression(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { id } = req.params as { id: string };
      const updates = req.body as Record<string, unknown>;

      const suppression = await suppressionRepo.findByIdAndTenant(id, tenantId);
      if (!suppression) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: "Suppression not found" } });
        return;
      }

      const safeUpdates: Record<string, unknown> = {};
      if (updates["status"] !== undefined) safeUpdates["status"] = updates["status"];
      if (updates["notes"] !== undefined) safeUpdates["notes"] = updates["notes"] ?? null;
      if (updates["expiresAt"] !== undefined) safeUpdates["expiresAt"] = updates["expiresAt"] ? new Date(updates["expiresAt"] as string) : null;

      await suppressionRepo.update(id, safeUpdates as Parameters<typeof suppressionRepo.update>[1]);

      await auditClient.publishEvent({
        eventType: "contact_suppression.updated",
        tenantId,
        metadata: { suppressionId: id, updates: Object.keys(safeUpdates) },
      });

      res.json({ data: await suppressionRepo.findByIdAndTenant(id, tenantId) });
    } catch (err) {
      handleError(res, err);
    }
  },

  // ─── Contact Health ───────────────────────────────────────────────────────

  async listContactHealth(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { channel, status, limit, offset } = req.query as Record<string, string>;
      const result = await healthRepo.list({
        tenantId,
        channel: channel as NotificationChannel | undefined,
        healthStatus: status,
        limit: limit ? parseInt(limit, 10) : 100,
        offset: offset ? parseInt(offset, 10) : 0,
      });
      res.json({ data: result.rows, count: result.count });
    } catch (err) {
      handleError(res, err);
    }
  },

  async getContactHealth(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { channel, contactValue } = req.params as { channel: string; contactValue: string };

      const decodedContact = decodeURIComponent(contactValue);
      const normalizedContact = normalizeContactValue(channel, decodedContact);

      const health = await healthRepo.findByContact(tenantId, channel as NotificationChannel, normalizedContact);

      const activeSuppressions = await suppressionRepo.findActive(tenantId, channel, normalizedContact);

      res.json({
        data: {
          health: health ?? null,
          activeSuppressions,
          normalizedContactValue: normalizedContact,
        },
      });
    } catch (err) {
      handleError(res, err);
    }
  },

  // ─── Contact Policies ─────────────────────────────────────────────────────

  async listPolicies(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const policies = await policyRepo.findAllByTenant(tenantId);
      res.json({ data: policies });
    } catch (err) {
      handleError(res, err);
    }
  },

  async createPolicy(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const {
        channel,
        blockSuppressedContacts,
        blockUnsubscribedContacts,
        blockComplainedContacts,
        blockBouncedContacts,
        blockInvalidContacts,
        blockCarrierRejectedContacts,
        allowManualOverride,
      } = req.body as Record<string, unknown>;

      if (channel && !VALID_CHANNELS.includes(channel as NotificationChannel)) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: `channel must be one of: ${VALID_CHANNELS.join(", ")}` } });
        return;
      }

      const policy = await policyRepo.create({
        tenantId,
        channel: channel as string | null ?? null,
        ...(blockSuppressedContacts !== undefined && { blockSuppressedContacts: Boolean(blockSuppressedContacts) }),
        ...(blockUnsubscribedContacts !== undefined && { blockUnsubscribedContacts: Boolean(blockUnsubscribedContacts) }),
        ...(blockComplainedContacts !== undefined && { blockComplainedContacts: Boolean(blockComplainedContacts) }),
        ...(blockBouncedContacts !== undefined && { blockBouncedContacts: Boolean(blockBouncedContacts) }),
        ...(blockInvalidContacts !== undefined && { blockInvalidContacts: Boolean(blockInvalidContacts) }),
        ...(blockCarrierRejectedContacts !== undefined && { blockCarrierRejectedContacts: Boolean(blockCarrierRejectedContacts) }),
        ...(allowManualOverride !== undefined && { allowManualOverride: Boolean(allowManualOverride) }),
      });

      await auditClient.publishEvent({
        eventType: "contact_policy.created",
        tenantId,
        channel: channel as string | undefined,
        metadata: { policyId: policy.id },
      });

      res.status(201).json({ data: policy });
    } catch (err) {
      handleError(res, err);
    }
  },

  async updatePolicy(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { id } = req.params as { id: string };
      const updates = req.body as Record<string, unknown>;

      const policy = await policyRepo.findByIdAndTenant(id, tenantId);
      if (!policy) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: "Contact policy not found" } });
        return;
      }

      const safeUpdates: Record<string, unknown> = {};
      const boolFields = [
        "blockSuppressedContacts", "blockUnsubscribedContacts", "blockComplainedContacts",
        "blockBouncedContacts", "blockInvalidContacts", "blockCarrierRejectedContacts", "allowManualOverride",
      ];
      for (const f of boolFields) {
        if (updates[f] !== undefined) safeUpdates[f] = Boolean(updates[f]);
      }
      if (updates["status"] !== undefined) safeUpdates["status"] = updates["status"];

      await policyRepo.update(id, safeUpdates as Parameters<typeof policyRepo.update>[1]);

      await auditClient.publishEvent({
        eventType: "contact_policy.updated",
        tenantId,
        metadata: { policyId: id, updated: Object.keys(safeUpdates) },
      });

      res.json({ data: await policyRepo.findByIdAndTenant(id, tenantId) });
    } catch (err) {
      handleError(res, err);
    }
  },
};
