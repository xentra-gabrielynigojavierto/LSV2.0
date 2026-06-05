import { Sequelize } from "sequelize";
import { AppConfig } from "../config";
import { logger } from "../shared/logger";
import { initTenantModel } from "./tenant.model";
import { initNotificationModel, Notification } from "./notification.model";
import { initNotificationAttemptModel, NotificationAttempt } from "./notification-attempt.model";
import { initProviderHealthModel } from "./provider-health.model";
import { initTenantProviderConfigModel } from "./tenant-provider-config.model";
import { initProviderWebhookLogModel } from "./provider-webhook-log.model";
import { initNotificationEventModel } from "./notification-event.model";
import { initRecipientContactHealthModel } from "./recipient-contact-health.model";
import { initDeliveryIssueModel } from "./delivery-issue.model";
import { initTemplateModel } from "./template.model";
import { initTemplateVersionModel } from "./template-version.model";
import { initTenantChannelProviderSettingModel } from "./tenant-channel-provider-setting.model";
import { initUsageMeterEventModel } from "./usage-meter-event.model";
import { initTenantBillingPlanModel } from "./tenant-billing-plan.model";
import { initTenantBillingRateModel } from "./tenant-billing-rate.model";
import { initTenantRateLimitPolicyModel } from "./tenant-rate-limit-policy.model";
import { initContactSuppressionModel } from "./contact-suppression.model";
import { initTenantContactPolicyModel } from "./tenant-contact-policy.model";
import { initTenantBrandingModel } from "./tenant-branding.model";

let sequelize: Sequelize | null = null;

export async function initDatabase(config: AppConfig, opts: { skipSync?: boolean } = {}): Promise<Sequelize | null> {
  const { host, port, name, user, password } = config.db;

  if (!host || !name || !user) {
    logger.warn("Database configuration is incomplete — skipping DB initialization");
    return null;
  }

  sequelize = new Sequelize(name, user, password, {
    host,
    port,
    dialect: "mysql",
    logging: (msg) => logger.debug(msg),
  });

  try {
    await sequelize.authenticate();
    logger.info("Database connection established", { host, port, name });
  } catch (err) {
    logger.error("Failed to connect to database", { error: String(err) });
    return null;
  }

  initTenantModel(sequelize);
  initNotificationModel(sequelize);
  initNotificationAttemptModel(sequelize);
  initProviderHealthModel(sequelize);
  initTenantProviderConfigModel(sequelize);
  initProviderWebhookLogModel(sequelize);
  initNotificationEventModel(sequelize);
  initRecipientContactHealthModel(sequelize);
  initDeliveryIssueModel(sequelize);
  initTemplateModel(sequelize);
  initTemplateVersionModel(sequelize);
  initTenantChannelProviderSettingModel(sequelize);
  initUsageMeterEventModel(sequelize);
  initTenantBillingPlanModel(sequelize);
  initTenantBillingRateModel(sequelize);
  initTenantRateLimitPolicyModel(sequelize);
  initContactSuppressionModel(sequelize);
  initTenantContactPolicyModel(sequelize);
  initTenantBrandingModel(sequelize);

  // Associations
  NotificationAttempt.belongsTo(Notification, { foreignKey: "notificationId", as: "notification" });
  Notification.hasMany(NotificationAttempt, { foreignKey: "notificationId", as: "attempts" });

  const isDev = config.nodeEnv !== "production";

  if (opts.skipSync) {
    logger.debug("Skipping sequelize.sync() — skipSync requested");
    return sequelize;
  }

  try {
    await sequelize.sync({ alter: isDev });
    logger.info("Models synchronized with database", { alter: isDev });
  } catch (err) {
    logger.error("Failed to sync models", { error: String(err) });
  }

  return sequelize;
}

export function getSequelize(): Sequelize {
  if (!sequelize) throw new Error("Database has not been initialized");
  return sequelize;
}
