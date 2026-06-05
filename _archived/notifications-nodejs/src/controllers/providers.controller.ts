import { Request, Response } from "express";
import {
  createTenantProviderConfig,
  updateTenantProviderConfig,
  listTenantProviderConfigs,
  getTenantProviderConfig,
  validateTenantProviderConfig,
  testTenantProviderConfig,
  activateTenantProviderConfig,
  deleteTenantProviderConfig,
} from "../services/tenant-provider-config.service";
import { NotificationAttemptRepository } from "../repositories/notification-attempt.repository";
import {
  listTenantChannelSettings,
  getTenantChannelSetting,
  updateTenantChannelSetting,
} from "../services/tenant-channel-setting.service";
import { PROVIDER_CATALOG } from "../integrations/providers/schemas/index";
import { NotificationChannel } from "../types";
import { logger } from "../shared/logger";

function handleError(res: Response, err: unknown): void {
  const e = err as { statusCode?: number; message?: string; details?: string[] };
  const statusCode = e.statusCode ?? 500;
  const message = e.message ?? "An unexpected error occurred";
  logger.error("Provider controller error", { statusCode, message });
  res.status(statusCode).json({ error: { code: "PROVIDER_ERROR", message, details: e.details } });
}

export const providersController = {
  listCatalog(_req: Request, res: Response): void {
    res.json({ data: PROVIDER_CATALOG });
  },

  async listConfigs(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const channel = req.query["channel"] as NotificationChannel | undefined;
      const configs = await listTenantProviderConfigs(tenantId, channel);
      res.json({ data: configs, count: configs.length });
    } catch (err) {
      handleError(res, err);
    }
  },

  async getConfig(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { configId } = req.params as { configId: string };
      const config = await getTenantProviderConfig(configId, tenantId);
      res.json({ data: config });
    } catch (err) {
      handleError(res, err);
    }
  },

  async createConfig(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const {
        channel,
        providerType,
        displayName,
        endpointConfig,
        senderConfig,
        webhookConfig,
        credentials,
        allowPlatformFallback,
        allowAutomaticFailover,
      } = req.body as Record<string, unknown>;

      if (!channel || !providerType || !displayName) {
        res.status(400).json({
          error: { code: "VALIDATION_ERROR", message: "channel, providerType, and displayName are required" },
        });
        return;
      }

      const config = await createTenantProviderConfig({
        tenantId,
        channel: channel as NotificationChannel,
        providerType: providerType as string,
        displayName: displayName as string,
        endpointConfig: endpointConfig as Record<string, unknown> | undefined,
        senderConfig: senderConfig as Record<string, unknown> | undefined,
        webhookConfig: webhookConfig as Record<string, unknown> | undefined,
        credentials: credentials as Record<string, unknown> | undefined,
        allowPlatformFallback: allowPlatformFallback as boolean | undefined,
        allowAutomaticFailover: allowAutomaticFailover as boolean | undefined,
      });

      res.status(201).json({ data: config });
    } catch (err) {
      handleError(res, err);
    }
  },

  async updateConfig(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { configId } = req.params as { configId: string };
      const updates = req.body as {
        displayName?: string;
        endpointConfig?: Record<string, unknown>;
        senderConfig?: Record<string, unknown>;
        webhookConfig?: Record<string, unknown>;
        credentials?: Record<string, unknown>;
        allowPlatformFallback?: boolean;
        allowAutomaticFailover?: boolean;
        status?: "active" | "inactive";
      };

      const config = await updateTenantProviderConfig(configId, tenantId, updates);
      res.json({ data: config });
    } catch (err) {
      handleError(res, err);
    }
  },

  async validateConfig(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { configId } = req.params as { configId: string };
      const result = await validateTenantProviderConfig(configId, tenantId);
      res.json({ data: result });
    } catch (err) {
      handleError(res, err);
    }
  },

  async testConfig(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { configId } = req.params as { configId: string };
      const { toEmail, subject, body } = req.body as { toEmail?: string; subject?: string; body?: string };
      const result = await testTenantProviderConfig(configId, tenantId, { toEmail, subject, body });
      const statusCode = result.success ? 200 : 422;
      res.status(statusCode).json({ data: result });
    } catch (err) {
      handleError(res, err);
    }
  },

  async activateConfig(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { configId } = req.params as { configId: string };
      const config = await activateTenantProviderConfig(configId, tenantId);
      res.json({ data: config });
    } catch (err) {
      handleError(res, err);
    }
  },

  async deactivateConfig(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { configId } = req.params as { configId: string };
      const config = await updateTenantProviderConfig(configId, tenantId, { status: "inactive" });
      res.json({ data: config });
    } catch (err) {
      handleError(res, err);
    }
  },

  async deleteConfig(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { configId } = req.params as { configId: string };
      await deleteTenantProviderConfig(configId, tenantId);
      res.status(204).send();
    } catch (err) {
      handleError(res, err);
    }
  },

  async listChannelSettings(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const settings = await listTenantChannelSettings(tenantId);
      res.json({ data: settings });
    } catch (err) {
      handleError(res, err);
    }
  },

  async getChannelSetting(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { channel } = req.params as { channel: string };
      const setting = await getTenantChannelSetting(tenantId, channel as NotificationChannel);
      if (!setting) {
        res.json({ data: null, message: `No channel setting configured for ${channel} — using platform_managed defaults` });
        return;
      }
      res.json({ data: setting });
    } catch (err) {
      handleError(res, err);
    }
  },

  async updateChannelSetting(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { channel } = req.params as { channel: string };
      const updates = req.body as {
        providerMode?: "platform_managed" | "tenant_managed";
        primaryTenantProviderConfigId?: string | null;
        fallbackTenantProviderConfigId?: string | null;
        allowPlatformFallback?: boolean;
        allowAutomaticFailover?: boolean;
      };

      const { data, errors } = await updateTenantChannelSetting(
        tenantId,
        channel as NotificationChannel,
        updates
      );

      if (errors.length > 0) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: "Validation failed", details: errors } });
        return;
      }

      res.json({ data });
    } catch (err) {
      handleError(res, err);
    }
  },

  async listConfigLogs(req: Request, res: Response): Promise<void> {
    try {
      const { configId } = req.params as { configId: string };
      const { limit, offset, status, from, to } = req.query as Record<string, string | undefined>;

      const attemptRepo = new NotificationAttemptRepository();
      const result = await attemptRepo.findByProviderConfigId(configId, {
        limit:  limit  ? parseInt(limit,  10) : undefined,
        offset: offset ? parseInt(offset, 10) : undefined,
        status: status || undefined,
        from:   from ? new Date(from) : undefined,
        to:     to   ? new Date(to)   : undefined,
      });

      const rows = result.rows.map((a) => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const notif = (a as any).notification as Record<string, unknown> | undefined;
        let recipient: string | null = null;
        if (notif?.recipientJson) {
          try {
            const r = JSON.parse(notif.recipientJson as string);
            recipient = r.email ?? r.phone ?? r.address ?? null;
          } catch {
            recipient = null;
          }
        }
        return {
          id:                  a.id,
          notificationId:      a.notificationId,
          attemptNumber:       a.attemptNumber,
          status:              a.status,
          provider:            a.provider,
          providerMessageId:   a.providerMessageId,
          failureCategory:     a.failureCategory,
          errorMessage:        a.errorMessage,
          startedAt:           a.startedAt,
          completedAt:         a.completedAt,
          platformFallbackUsed: a.platformFallbackUsed,
          channel:             notif?.channel   ?? null,
          renderedSubject:     notif?.renderedSubject ?? null,
          templateKey:         notif?.templateKey ?? null,
          recipient,
          notificationCreatedAt: notif?.createdAt ?? null,
        };
      });

      res.json({ data: { rows, total: result.count } });
    } catch (err) {
      handleError(res, err);
    }
  },
};
