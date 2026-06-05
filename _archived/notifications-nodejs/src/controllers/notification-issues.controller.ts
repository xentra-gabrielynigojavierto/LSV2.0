import { Request, Response, NextFunction } from "express";
import { DeliveryIssueRepository } from "../repositories/delivery-issue.repository";

const repo = new DeliveryIssueRepository();

export const notificationIssuesController = {
  async list(req: Request, res: Response, next: NextFunction): Promise<void> {
    const { id: notificationId } = req.params as { id: string };
    const tenantId = req.tenantId;

    try {
      const issues = await repo.findByNotificationId(notificationId, tenantId);
      res.status(200).json({ data: issues });
    } catch (err) {
      next(err);
    }
  },
};
