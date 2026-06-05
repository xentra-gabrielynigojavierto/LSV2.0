import { DeliveryIssue, DeliveryIssueType } from "../models/delivery-issue.model";
import { NotificationChannel } from "../types";

interface CreateDeliveryIssueInput {
  tenantId: string;
  notificationId: string;
  notificationAttemptId?: string | null;
  channel: NotificationChannel;
  provider: string;
  issueType: DeliveryIssueType;
  recommendedAction?: string | null;
  detailsJson?: string | null;
}

export class DeliveryIssueRepository {
  async findExisting(
    tenantId: string,
    notificationId: string,
    issueType: DeliveryIssueType
  ): Promise<DeliveryIssue | null> {
    return DeliveryIssue.findOne({
      where: { tenantId, notificationId, issueType, status: "open" },
    });
  }

  async createIfNotExists(input: CreateDeliveryIssueInput): Promise<DeliveryIssue | null> {
    const existing = await this.findExisting(input.tenantId, input.notificationId, input.issueType);
    if (existing) return existing;

    return DeliveryIssue.create({
      ...input,
      notificationAttemptId: input.notificationAttemptId ?? null,
      recommendedAction: input.recommendedAction ?? null,
      detailsJson: input.detailsJson ?? null,
    });
  }

  async findByNotificationId(notificationId: string, tenantId: string): Promise<DeliveryIssue[]> {
    return DeliveryIssue.findAll({
      where: { notificationId, tenantId },
      order: [["createdAt", "DESC"]],
    });
  }
}
