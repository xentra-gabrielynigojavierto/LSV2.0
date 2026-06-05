import { NotificationAttempt } from "../models/notification-attempt.model";
import { Notification } from "../models/notification.model";
import { NormalizedEventType } from "../models/notification-event.model";
import { logger } from "../shared/logger";

// Attempt statuses that count as terminal — do not regress these
const ATTEMPT_TERMINAL: string[] = ["delivered", "failed"];

// Notification statuses that are final — do not regress
const NOTIFICATION_TERMINAL: string[] = ["sent", "failed"];

// Which normalized event types map to which attempt status
const NORMALIZED_TO_ATTEMPT_STATUS: Partial<Record<NormalizedEventType, string>> = {
  accepted: "sending",
  queued: "sending",
  sent: "sent",
  delivered: "sent", // delivered is a terminal success for the attempt record
  failed: "failed",
  undeliverable: "failed",
  bounced: "failed",
  rejected: "failed",
};

// Which normalized event types map to which notification status
const NORMALIZED_TO_NOTIFICATION_STATUS: Partial<Record<NormalizedEventType, string>> = {
  accepted: "processing",
  queued: "processing",
  deferred: "processing",
  sent: "sent",
  delivered: "sent",
  failed: "failed",
  undeliverable: "failed",
  bounced: "failed",
  rejected: "failed",
};

export class DeliveryStatusService {
  async updateAttemptFromEvent(
    attemptId: string,
    normalizedEventType: NormalizedEventType
  ): Promise<void> {
    const attempt = await NotificationAttempt.findByPk(attemptId);
    if (!attempt) return;

    // Never regress a terminal state
    if (ATTEMPT_TERMINAL.includes(attempt.status)) {
      logger.debug("Skipping attempt status update — already in terminal state", {
        attemptId,
        currentStatus: attempt.status,
        event: normalizedEventType,
      });
      return;
    }

    const newStatus = NORMALIZED_TO_ATTEMPT_STATUS[normalizedEventType];
    if (!newStatus) return;

    await NotificationAttempt.update(
      {
        status: newStatus as any,
        ...(newStatus === "failed" || newStatus === "sent"
          ? { completedAt: new Date() }
          : {}),
      },
      { where: { id: attemptId } }
    );

    logger.debug("Updated attempt status from webhook event", {
      attemptId,
      newStatus,
      event: normalizedEventType,
    });
  }

  async updateNotificationFromEvent(
    notificationId: string,
    normalizedEventType: NormalizedEventType
  ): Promise<void> {
    const notification = await Notification.findByPk(notificationId);
    if (!notification) return;

    // Never regress a terminal state
    if (NOTIFICATION_TERMINAL.includes(notification.status)) {
      // Exception: 'sent' can become 'failed' for hard bounces/permanent failures
      const isFinalFailure =
        normalizedEventType === "bounced" ||
        normalizedEventType === "undeliverable" ||
        normalizedEventType === "rejected";

      if (notification.status === "failed") {
        logger.debug("Skipping notification status update — already failed", {
          notificationId,
          event: normalizedEventType,
        });
        return;
      }

      if (notification.status === "sent" && !isFinalFailure) {
        logger.debug("Skipping notification status update — already sent (no hard failure)", {
          notificationId,
          event: normalizedEventType,
        });
        return;
      }
    }

    const newStatus = NORMALIZED_TO_NOTIFICATION_STATUS[normalizedEventType];
    if (!newStatus) return;

    await Notification.update(
      { status: newStatus as any },
      { where: { id: notificationId } }
    );

    logger.debug("Updated notification status from webhook event", {
      notificationId,
      newStatus,
      event: normalizedEventType,
    });
  }
}
