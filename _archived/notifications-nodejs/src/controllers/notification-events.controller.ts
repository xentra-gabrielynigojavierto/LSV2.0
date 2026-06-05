import { Request, Response, NextFunction } from "express";
import { NotificationEventRepository } from "../repositories/notification-event.repository";

const repo = new NotificationEventRepository();

export const notificationEventsController = {
  async list(req: Request, res: Response, next: NextFunction): Promise<void> {
    const { id: notificationId } = req.params as { id: string };
    const tenantId = req.tenantId;

    try {
      const events = await repo.findByNotificationId(notificationId);

      // Ensure tenant-scoped access: if any event has a tenantId, verify it matches
      const filtered = events.filter(
        (e) => e.tenantId === null || e.tenantId === tenantId
      );

      res.status(200).json({ data: filtered });
    } catch (err) {
      next(err);
    }
  },
};
