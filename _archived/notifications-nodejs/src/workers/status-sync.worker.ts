import "../types";
import { logger } from "../shared/logger";
import { loadConfig } from "../config";
import { initDatabase } from "../models";
import { NotificationRepository } from "../repositories/notification.repository";
import { NotificationAttemptRepository } from "../repositories/notification-attempt.repository";
import { TenantProviderConfigRepository } from "../repositories/tenant-provider-config.repository";
import { SendGridEmailProviderAdapter } from "../integrations/providers/adapters/sendgrid.adapter";
import { decrypt } from "../shared/crypto.service";

const notifRepo   = new NotificationRepository();
const attemptRepo = new NotificationAttemptRepository();
const configRepo  = new TenantProviderConfigRepository();

const LOOKBACK_HOURS    = 24;
const INTERVAL_MINUTES  = parseInt(process.env["STATUS_SYNC_INTERVAL_MINUTES"] ?? "2", 10);
const PER_REQUEST_DELAY = 600; // ms between SendGrid API calls to stay well within rate limits

async function syncAcceptedStatuses(): Promise<void> {
  const pending = await notifRepo.listPendingStatusSync({
    providerUsed:  "sendgrid",
    lookbackHours: LOOKBACK_HOURS,
    limit:         50,
  });

  if (pending.length === 0) {
    logger.debug("Status sync: no accepted SendGrid notifications to check");
    return;
  }

  logger.info(`Status sync: checking ${pending.length} accepted notification(s)`);

  for (const notif of pending) {
    try {
      const recipientJson = JSON.parse(notif.recipientJson ?? "{}") as Record<string, unknown>;
      const toEmail = (recipientJson["email"] ?? recipientJson["to"]) as string | undefined;
      if (!toEmail) {
        logger.warn("Status sync: no email in recipientJson", { notifId: notif.id, recipientJson: notif.recipientJson });
        continue;
      }

      if (!notif.providerConfigId) {
        logger.warn("Status sync: no providerConfigId on notification", { notifId: notif.id });
        continue;
      }

      const config = await configRepo.findById(notif.providerConfigId);
      if (!config?.credentialReference) {
        logger.warn("Status sync: provider config not found or missing credentials", {
          notifId: notif.id,
          configId: notif.providerConfigId,
          found: !!config,
        });
        continue;
      }

      const credentials = JSON.parse(decrypt(config.credentialReference)) as Record<string, unknown>;
      const apiKey = credentials["apiKey"] as string | undefined;
      if (!apiKey) {
        logger.warn("Status sync: no apiKey in decrypted credentials", { notifId: notif.id });
        continue;
      }

      const adapter = new SendGridEmailProviderAdapter({
        apiKey,
        defaultFromEmail: "",
        defaultFromName:  "",
      });

      logger.info("Status sync: querying SendGrid for status", { notifId: notif.id, toEmail });
      const sgStatus = await adapter.queryMessageStatus(toEmail);
      logger.info("Status sync poll result", { notifId: notif.id, toEmail, sgStatus });

      if (!sgStatus || sgStatus === "processing" || sgStatus === "deferred") {
        logger.info("Status sync: no actionable status yet", { notifId: notif.id, sgStatus: sgStatus ?? "null/unavailable" });
        continue;
      }

      if (sgStatus === "delivered") {
        await notifRepo.update(notif.id, { status: "sent" });
        const attempts = await attemptRepo.findByNotificationId(notif.id);
        for (const a of attempts) {
          await attemptRepo.complete(a.id, { status: "sent" });
        }
        logger.info("Status sync: marked delivered", { notifId: notif.id, toEmail });
      } else if (["not_delivered", "blocked", "bounced", "spam_report"].includes(sgStatus)) {
        const failureCategory = sgStatus === "bounced" ? "invalid_recipient" : "non_retryable_failure";
        await notifRepo.update(notif.id, {
          status:           "failed",
          failureCategory,
          lastErrorMessage: `SendGrid: ${sgStatus}`,
        });
        const attempts = await attemptRepo.findByNotificationId(notif.id);
        for (const a of attempts) {
          await attemptRepo.complete(a.id, {
            status:       "failed",
            errorMessage: `SendGrid: ${sgStatus}`,
          });
        }
        logger.info("Status sync: marked failed", { notifId: notif.id, toEmail, sgStatus });
      }
    } catch (err) {
      logger.warn("Status sync: error processing notification", {
        notifId: notif.id,
        error: String(err),
      });
    }

    await new Promise(resolve => setTimeout(resolve, PER_REQUEST_DELAY));
  }
}

async function run(): Promise<void> {
  const config = loadConfig();
  const db = await initDatabase(config, { skipSync: true });
  if (!db) {
    logger.warn("Status sync worker: database unavailable — worker will exit");
    return;
  }

  logger.info("Status sync worker starting", {
    intervalMinutes: INTERVAL_MINUTES,
    lookbackHours:   LOOKBACK_HOURS,
  });

  await syncAcceptedStatuses();

  setInterval(() => {
    syncAcceptedStatuses().catch(err => {
      logger.error("Status sync cycle failed", { error: String(err) });
    });
  }, INTERVAL_MINUTES * 60 * 1000);
}

run().catch(err => {
  logger.error("Status sync worker crashed", { error: String(err) });
  process.exit(1);
});
