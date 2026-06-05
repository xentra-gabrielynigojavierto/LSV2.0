import { Request, Response, NextFunction } from "express";
import { NotificationService } from "../services/notification.service";
import { validateSubmitNotification } from "../validators/notification.validator";
import { NotificationChannel } from "../types";
import { NotificationStatus } from "../models/notification.model";
import { logger } from "../shared/logger";

let notificationService: NotificationService | null = null;

export function setNotificationService(service: NotificationService): void {
  notificationService = service;
}

function requireService(res: Response): NotificationService | null {
  if (!notificationService) {
    res.status(503).json({
      error: { code: "SERVICE_UNAVAILABLE", message: "Notification service is not initialized" },
    });
    return null;
  }
  return notificationService;
}

export const notificationsController = {
  async create(req: Request, res: Response, next: NextFunction): Promise<void> {
    const service = requireService(res);
    if (!service) return;

    const body = req.body as Record<string, unknown>;
    const validation = validateSubmitNotification(body);

    if (!validation.valid) {
      res.status(400).json({
        error: { code: "VALIDATION_ERROR", message: "Invalid request", details: validation.errors },
      });
      return;
    }

    try {
      const result = await service.submit({
        tenantId: req.tenantId,
        channel: body["channel"] as NotificationChannel,
        recipient: body["recipient"] as any,
        message: body["message"] as any,
        metadata: body["metadata"] as Record<string, unknown> | undefined,
        idempotencyKey: body["idempotencyKey"] as string | undefined,
        templateKey: body["templateKey"] as string | undefined,
        templateData: body["templateData"] as Record<string, unknown> | undefined,
      });

      const statusCode = result.idempotent ? 200 : 202;
      res.status(statusCode).json({ data: result });
    } catch (err) {
      next(err);
    }
  },

  async get(req: Request, res: Response, next: NextFunction): Promise<void> {
    const service = requireService(res);
    if (!service) return;

    const { id } = req.params as { id: string };

    try {
      const notification = await service.getById(id, req.tenantId);
      if (!notification) {
        res.status(404).json({
          error: { code: "NOT_FOUND", message: `Notification ${id} not found` },
        });
        return;
      }
      res.status(200).json({ data: notification });
    } catch (err) {
      next(err);
    }
  },

  async list(req: Request, res: Response, next: NextFunction): Promise<void> {
    const service = requireService(res);
    if (!service) return;

    const query = req.query as Record<string, string | undefined>;

    const channel = query["channel"] as NotificationChannel | undefined;
    const status = query["status"] as NotificationStatus | undefined;
    const limit = query["limit"] ? parseInt(query["limit"], 10) : 20;
    const offset = query["offset"]
      ? parseInt(query["offset"], 10)
      : query["page"]
      ? (parseInt(query["page"], 10) - 1) * limit
      : 0;

    if (channel && !["email", "sms", "push", "in-app"].includes(channel)) {
      res.status(400).json({
        error: { code: "VALIDATION_ERROR", message: `Invalid channel: ${channel}` },
      });
      return;
    }

    try {
      const result = await service.list({
        tenantId: req.tenantId,
        channel,
        status,
        limit: isNaN(limit) ? 20 : limit,
        offset: isNaN(offset) ? 0 : offset,
      });

      res.status(200).json({
        data: result.rows,
        meta: {
          total: result.count,
          limit: isNaN(limit) ? 20 : limit,
          offset: isNaN(offset) ? 0 : offset,
        },
      });
    } catch (err) {
      next(err);
    }
  },

  async stats(req: Request, res: Response, next: NextFunction): Promise<void> {
    const service = requireService(res);
    if (!service) return;
    try {
      const data = await service.getStats(req.tenantId);
      res.status(200).json({ data });
    } catch (err) {
      next(err);
    }
  },

  update(_req: Request, res: Response): void {
    res.status(501).json({ error: { code: "NOT_IMPLEMENTED", message: "Not implemented" } });
  },

  cancel(_req: Request, res: Response): void {
    res.status(501).json({ error: { code: "NOT_IMPLEMENTED", message: "Not implemented" } });
  },
};
