import { TenantProviderConfigRepository } from "../repositories/tenant-provider-config.repository";
import { NotificationRepository } from "../repositories/notification.repository";
import { NotificationAttemptRepository } from "../repositories/notification-attempt.repository";
import { TenantProviderConfig } from "../models/tenant-provider-config.model";
import { auditClient } from "../integrations/audit/audit.client";
import { encrypt, decrypt, maskSecret } from "../shared/crypto.service";
import { logger } from "../shared/logger";
import { NotificationChannel } from "../types";
import {
  validateSendGridCredentials,
  validateSendGridEndpointConfig,
} from "../integrations/providers/schemas/sendgrid.schema";
import {
  validateSmtpCredentials,
  validateSmtpEndpointConfig,
} from "../integrations/providers/schemas/smtp.schema";
import {
  validateTwilioCredentials,
  validateTwilioEndpointConfig,
} from "../integrations/providers/schemas/twilio.schema";
import { SendGridEmailProviderAdapter } from "../integrations/providers/adapters/sendgrid.adapter";
import { SmtpEmailProviderAdapter } from "../integrations/providers/adapters/smtp.adapter";
import { TwilioSmsProviderAdapter } from "../integrations/providers/adapters/twilio.adapter";
import { isSupportedEmailProvider, isSupportedSmsProvider } from "../integrations/providers/schemas/index";

const repo = new TenantProviderConfigRepository();
const notifRepo = new NotificationRepository();
const attemptRepo = new NotificationAttemptRepository();

export interface CreateTenantProviderConfigInput {
  tenantId: string | undefined;
  channel: NotificationChannel;
  providerType: string;
  displayName: string;
  endpointConfig?: Record<string, unknown>;
  senderConfig?: Record<string, unknown>;
  webhookConfig?: Record<string, unknown>;
  credentials?: Record<string, unknown>;
  allowPlatformFallback?: boolean;
  allowAutomaticFailover?: boolean;
}

export interface SafeProviderConfigView {
  id: string;
  tenantId: string | null;
  channel: string;
  providerType: string;
  ownershipMode: string;
  displayName: string;
  isActive: boolean;
  isPrimary: boolean;
  isFallback: boolean;
  allowAutomaticFailover: boolean;
  allowPlatformFallback: boolean;
  status: string;
  endpointConfig: Record<string, unknown> | null;
  senderConfig: Record<string, unknown> | null;
  webhookConfig: Record<string, unknown> | null;
  credentialStatus: string;
  validationStatus: string;
  healthStatus: string;
  lastValidatedAt: Date | null;
  createdAt: Date;
  updatedAt: Date;
}

function safeView(config: TenantProviderConfig): SafeProviderConfigView {
  return {
    id: config.id,
    tenantId: config.tenantId,
    channel: config.channel,
    providerType: config.providerType,
    ownershipMode: config.ownershipMode,
    displayName: config.displayName,
    isActive: config.isActive,
    isPrimary: config.isPrimary,
    isFallback: config.isFallback,
    allowAutomaticFailover: config.allowAutomaticFailover,
    allowPlatformFallback: config.allowPlatformFallback,
    status: config.status,
    endpointConfig: config.endpointConfigJson ? (JSON.parse(config.endpointConfigJson) as Record<string, unknown>) : null,
    senderConfig: config.senderConfigJson ? (JSON.parse(config.senderConfigJson) as Record<string, unknown>) : null,
    webhookConfig: config.webhookConfigJson ? (JSON.parse(config.webhookConfigJson) as Record<string, unknown>) : null,
    credentialStatus: maskSecret(config.credentialReference),
    validationStatus: config.validationStatus,
    healthStatus: config.healthStatus,
    lastValidatedAt: config.lastValidatedAt,
    createdAt: config.createdAt,
    updatedAt: config.updatedAt,
  };
}

function validateProviderType(channel: NotificationChannel, providerType: string): string[] {
  if (channel === "email" && !isSupportedEmailProvider(providerType)) {
    return [`Unsupported email provider: ${providerType}. Supported: sendgrid, smtp`];
  }
  if (channel === "sms" && !isSupportedSmsProvider(providerType)) {
    return [`Unsupported sms provider: ${providerType}. Supported: twilio`];
  }
  if (channel !== "email" && channel !== "sms") {
    return [`BYOP is not supported for channel: ${channel}`];
  }
  return [];
}

function validateProviderConfig(
  channel: NotificationChannel,
  providerType: string,
  endpointConfig: Record<string, unknown>,
  credentials: Record<string, unknown>
): string[] {
  const errors: string[] = [];

  if (channel === "email") {
    if (providerType === "sendgrid") {
      errors.push(...validateSendGridCredentials(credentials));
      errors.push(...validateSendGridEndpointConfig(endpointConfig));
    } else if (providerType === "smtp") {
      errors.push(...validateSmtpCredentials(credentials));
      errors.push(...validateSmtpEndpointConfig(endpointConfig));
    }
  } else if (channel === "sms") {
    if (providerType === "twilio") {
      errors.push(...validateTwilioCredentials(credentials));
      errors.push(...validateTwilioEndpointConfig(endpointConfig));
    }
  }

  return errors;
}

export async function createTenantProviderConfig(
  input: CreateTenantProviderConfigInput
): Promise<SafeProviderConfigView> {
  const { tenantId, channel, providerType, displayName } = input;

  const typeErrors = validateProviderType(channel, providerType);
  if (typeErrors.length > 0) throw Object.assign(new Error(typeErrors.join("; ")), { statusCode: 400, details: typeErrors });

  const endpointConfig = input.endpointConfig ?? {};
  const credentials = input.credentials ?? {};

  // Encrypt credentials and store reference
  let credentialReference: string | null = null;
  if (Object.keys(credentials).length > 0) {
    const encrypted = encrypt(JSON.stringify(credentials));
    credentialReference = encrypted;
  }

  const config = await repo.create({
    tenantId,
    channel,
    providerType,
    displayName,
    endpointConfigJson: Object.keys(endpointConfig).length > 0 ? JSON.stringify(endpointConfig) : null,
    senderConfigJson: input.senderConfig ? JSON.stringify(input.senderConfig) : null,
    webhookConfigJson: input.webhookConfig ? JSON.stringify(input.webhookConfig) : null,
    credentialReference,
    allowPlatformFallback: input.allowPlatformFallback ?? true,
    allowAutomaticFailover: input.allowAutomaticFailover ?? true,
  });

  await auditClient.publishEvent({
    eventType: "tenant_provider_config.created",
    tenantId,
    metadata: { configId: config.id, providerType, channel },
  });

  return safeView(config);
}

export async function updateTenantProviderConfig(
  id: string,
  tenantId: string | undefined,
  updates: {
    displayName?: string;
    endpointConfig?: Record<string, unknown>;
    senderConfig?: Record<string, unknown>;
    webhookConfig?: Record<string, unknown>;
    credentials?: Record<string, unknown>;
    allowPlatformFallback?: boolean;
    allowAutomaticFailover?: boolean;
    status?: "active" | "inactive";
  }
): Promise<SafeProviderConfigView> {
  const config = await repo.findByIdAndTenant(id, tenantId);
  if (!config) throw Object.assign(new Error("Provider config not found"), { statusCode: 404 });

  const repoUpdates: Parameters<typeof repo.update>[1] = {};

  if (updates.displayName) repoUpdates.displayName = updates.displayName;
  if (updates.endpointConfig) repoUpdates.endpointConfigJson = JSON.stringify(updates.endpointConfig);
  if (updates.senderConfig) repoUpdates.senderConfigJson = JSON.stringify(updates.senderConfig);
  if (updates.webhookConfig) repoUpdates.webhookConfigJson = JSON.stringify(updates.webhookConfig);
  if (updates.allowPlatformFallback !== undefined) repoUpdates.allowPlatformFallback = updates.allowPlatformFallback;
  if (updates.allowAutomaticFailover !== undefined) repoUpdates.allowAutomaticFailover = updates.allowAutomaticFailover;
  if (updates.status) repoUpdates.status = updates.status;

  if (updates.credentials && Object.keys(updates.credentials).length > 0) {
    repoUpdates.credentialReference = encrypt(JSON.stringify(updates.credentials));
    // Invalidate validation on credential rotation
    repoUpdates.validationStatus = "not_validated";
    await auditClient.publishEvent({
      eventType: "tenant_provider_credentials.rotated",
      tenantId,
      metadata: { configId: id, providerType: config.providerType },
    });
  }

  // If deactivating, mark inactive
  if (updates.status === "inactive") {
    repoUpdates.isActive = false;
    await auditClient.publishEvent({
      eventType: "tenant_provider_config.deactivated",
      tenantId,
      metadata: { configId: id },
    });
  }

  await repo.update(id, repoUpdates);

  await auditClient.publishEvent({
    eventType: "tenant_provider_config.updated",
    tenantId,
    metadata: { configId: id },
  });

  return safeView((await repo.findByIdAndTenant(id, tenantId))!);
}

export async function listTenantProviderConfigs(
  tenantId: string | undefined,
  channel?: NotificationChannel
): Promise<SafeProviderConfigView[]> {
  const configs = await repo.findByTenant(tenantId, channel);
  return configs.map(safeView);
}

export async function getTenantProviderConfig(
  id: string,
  tenantId: string | undefined
): Promise<SafeProviderConfigView> {
  const config = await repo.findByIdAndTenant(id, tenantId);
  if (!config) throw Object.assign(new Error("Provider config not found"), { statusCode: 404 });
  return safeView(config);
}

export async function validateTenantProviderConfig(
  id: string,
  tenantId: string | undefined
): Promise<{ valid: boolean; errors: string[] }> {
  const config = await repo.findByIdAndTenant(id, tenantId);
  if (!config) throw Object.assign(new Error("Provider config not found"), { statusCode: 404 });

  const endpointConfig: Record<string, unknown> = config.endpointConfigJson
    ? (JSON.parse(config.endpointConfigJson) as Record<string, unknown>)
    : {};

  let credentials: Record<string, unknown> = {};
  if (config.credentialReference) {
    try {
      credentials = JSON.parse(decrypt(config.credentialReference)) as Record<string, unknown>;
    } catch {
      await repo.update(id, { validationStatus: "invalid", lastValidatedAt: new Date() });
      await auditClient.publishEvent({
        eventType: "tenant_provider_config.validation_failed",
        tenantId,
        metadata: { configId: id, reason: "credential_decryption_failed" },
      });
      return { valid: false, errors: ["Failed to decrypt stored credentials"] };
    }
  }

  const typeErrors = validateProviderType(config.channel, config.providerType);
  if (typeErrors.length > 0) {
    await repo.update(id, { validationStatus: "invalid", lastValidatedAt: new Date() });
    return { valid: false, errors: typeErrors };
  }

  const configErrors = validateProviderConfig(config.channel, config.providerType, endpointConfig, credentials);
  if (configErrors.length > 0) {
    await repo.update(id, { validationStatus: "invalid", lastValidatedAt: new Date() });
    await auditClient.publishEvent({
      eventType: "tenant_provider_config.validation_failed",
      tenantId,
      metadata: { configId: id, errors: configErrors },
    });
    return { valid: false, errors: configErrors };
  }

  await repo.update(id, { validationStatus: "valid", lastValidatedAt: new Date() });
  await auditClient.publishEvent({
    eventType: "tenant_provider_config.validation_succeeded",
    tenantId,
    metadata: { configId: id, providerType: config.providerType },
  });

  return { valid: true, errors: [] };
}

export async function testTenantProviderConfig(
  id: string,
  tenantId: string | undefined,
  testPayload?: { toEmail?: string; subject?: string; body?: string }
): Promise<{ success: boolean; latencyMs?: number; message: string }> {
  const config = await repo.findByIdAndTenant(id, tenantId);
  if (!config) throw Object.assign(new Error("Provider config not found"), { statusCode: 404 });

  if (config.validationStatus !== "valid") {
    return { success: false, message: "Config must be validated before testing. Call /validate first." };
  }

  const endpointConfig: Record<string, unknown> = config.endpointConfigJson
    ? (JSON.parse(config.endpointConfigJson) as Record<string, unknown>)
    : {};

  let credentials: Record<string, unknown> = {};
  if (config.credentialReference) {
    try {
      credentials = JSON.parse(decrypt(config.credentialReference)) as Record<string, unknown>;
    } catch {
      return { success: false, message: "Failed to decrypt credentials" };
    }
  }

  // When a recipient email is supplied, send a real test message instead of a health check
  const sendRealEmail = config.channel === "email" && testPayload?.toEmail;

  try {
    if (sendRealEmail) {
      const fromEmail = endpointConfig["fromEmail"] as string;
      const fromName  = (endpointConfig["fromName"] as string) ?? "";
      const from      = fromName ? `${fromName} <${fromEmail}>` : fromEmail;

      const emailPayload = {
        to:      testPayload!.toEmail!,
        from,
        subject: testPayload?.subject ?? "LegalSynq — Test Email",
        body:    testPayload?.body    ?? "This is a test email sent from the LegalSynq Notifications platform.",
      };

      let sendResult: { success: boolean; providerMessageId?: string; failure?: { message?: string } };

      if (config.providerType === "sendgrid") {
        const adapter = new SendGridEmailProviderAdapter({
          apiKey: credentials["apiKey"] as string,
          defaultFromEmail: fromEmail,
          defaultFromName: fromName,
        });
        sendResult = await adapter.send(emailPayload);
      } else if (config.providerType === "smtp") {
        const adapter = new SmtpEmailProviderAdapter({
          host:     endpointConfig["host"] as string,
          port:     Number(endpointConfig["port"]),
          secure:   (endpointConfig["secure"] as boolean) ?? false,
          username: credentials["username"] as string,
          password: credentials["password"] as string,
          fromEmail,
          fromName: fromName || undefined,
        });
        sendResult = await adapter.send(emailPayload);
      } else {
        return { success: false, message: `Sending test emails is not supported for provider: ${config.providerType}` };
      }

      // Persist a Notification + Attempt record so the send shows in delivery/provider logs.
      // Use nil UUID '00000000-...' as a platform sentinel when the config has no tenant scope.
      // NOTE: SendGrid returns 202 Accepted synchronously; actual delivery events (blocks, bounces)
      // arrive asynchronously via webhook. We use "accepted" (not "sent") to reflect that we only
      // know the provider received the request — not that it was delivered.
      const PLATFORM_TENANT = "00000000-0000-0000-0000-000000000000";
      const attemptStatus: "sent" | "failed" = sendResult.success ? "sent" : "failed";
      // "accepted" = provider acknowledged our request; delivery outcome requires webhook confirmation
      const notifStatus: "accepted" | "failed" = sendResult.success ? "accepted" : "failed";
      let persistedNotifId: string | null = null;
      let persistedAttemptId: string | null = null;
      try {
        const notif = await notifRepo.create({
          tenantId:             config.tenantId ?? PLATFORM_TENANT,
          channel:              config.channel as NotificationChannel,
          recipientJson:        JSON.stringify({ email: testPayload!.toEmail }),
          messageJson:          JSON.stringify({
            subject: testPayload?.subject ?? "LegalSynq — Test Email",
            body:    testPayload?.body    ?? "This is a test email sent from the LegalSynq Notifications platform.",
          }),
          metadataJson:         JSON.stringify({ testSend: true }),
          renderedSubject:      testPayload?.subject ?? "LegalSynq — Test Email",
          providerConfigId:     config.id,
          providerOwnershipMode: config.ownershipMode,
        });
        persistedNotifId = notif.id;
        await notifRepo.update(notif.id, {
          status:       notifStatus,
          providerUsed: config.providerType,
          lastErrorMessage: sendResult.success ? null : (sendResult.failure?.message ?? null),
        });
        const attempt = await attemptRepo.create({
          tenantId:             config.tenantId ?? PLATFORM_TENANT,
          notificationId:       notif.id,
          attemptNumber:        1,
          provider:             config.providerType,
          providerConfigId:     config.id,
          providerOwnershipMode: config.ownershipMode,
        });
        persistedAttemptId = attempt.id;
        await attemptRepo.complete(attempt.id, {
          status:            attemptStatus,
          providerMessageId: sendResult.providerMessageId ?? null,
          errorMessage:      sendResult.success ? null : (sendResult.failure?.message ?? null),
        });
      } catch (recordErr) {
        logger.warn("Failed to persist test-send notification record", { configId: id, error: String(recordErr) });
      }

      // Fire-and-forget: poll SendGrid's Email Activity API at 8 s then 30 s to capture the
      // real delivery outcome (blocked, bounced, delivered) and backfill the record.
      if (sendResult.success && config.providerType === "sendgrid" && persistedNotifId && persistedAttemptId) {
        const sgAdapter = new SendGridEmailProviderAdapter({
          apiKey:            credentials["apiKey"] as string,
          defaultFromEmail:  fromEmail,
          defaultFromName:   fromName,
        });
        const notifId   = persistedNotifId;
        const attemptId = persistedAttemptId;
        const toEmail   = testPayload!.toEmail!;
        (async () => {
          for (const delayMs of [8_000, 30_000]) {
            await new Promise(resolve => setTimeout(resolve, delayMs));
            try {
              const sgStatus = await sgAdapter.queryMessageStatus(toEmail);
              logger.debug("SendGrid status poll result", { notifId, toEmail, sgStatus, delayMs });
              if (sgStatus === null) continue; // not visible yet or API unavailable — try next poll
              if (sgStatus === "delivered") {
                await notifRepo.update(notifId, { status: "sent" });
                await attemptRepo.complete(attemptId, { status: "sent" });
                logger.info("Test send confirmed delivered", { notifId, toEmail });
                return;
              }
              if (["not_delivered", "blocked", "bounced", "spam_report"].includes(sgStatus)) {
                const failureCategory = sgStatus === "bounced" ? "invalid_recipient" : "non_retryable_failure";
                await notifRepo.update(notifId, {
                  status:           "failed",
                  failureCategory,
                  lastErrorMessage: `SendGrid: ${sgStatus}`,
                });
                await attemptRepo.complete(attemptId, {
                  status:       "failed",
                  errorMessage: `SendGrid: ${sgStatus}`,
                });
                logger.info("Test send marked failed from SendGrid status", { notifId, toEmail, sgStatus });
                return;
              }
              // "processing" or "deferred" — let the next poll decide
            } catch (pollErr) {
              logger.debug("SendGrid status poll error", { notifId, error: String(pollErr) });
            }
          }
        })().catch(err => logger.debug("Background SendGrid poll crashed", { error: String(err) }));
      }

      if (sendResult.success) {
        await repo.update(id, { healthStatus: "healthy" });
        return {
          success: true,
          message: `Test email accepted by ${config.providerType} for delivery to ${testPayload!.toEmail}. ` +
            `If the email does not arrive or your provider shows it as blocked, the most common cause is an unverified sender address — ` +
            `go to SendGrid → Settings → Sender Authentication and verify the From email. ` +
            `Check the SendGrid Activity Feed for the real delivery status (blocks, bounces, and spam reports appear there first).`,
        };
      } else {
        const raw = sendResult.failure?.message ?? "";
        let hint = "";
        if (raw.includes("554") || raw.includes("not accepted") || raw.toLowerCase().includes("blocked")) {
          hint = " This often means the sender email is not a verified sender identity in SendGrid — go to Settings → Sender Authentication.";
        } else if (raw.includes("401") || raw.includes("403") || raw.toLowerCase().includes("api key") || raw.toLowerCase().includes("auth")) {
          hint = " Check that the API key in this provider config has 'Mail Send' permission.";
        } else if (raw.includes("400")) {
          hint = " The request was rejected by the provider — check that the From Email and API key are correct.";
        }
        return { success: false, message: (sendResult.failure?.message ?? "Send failed — check provider logs") + hint };
      }
    }

    // No recipient supplied — fall back to health check
    let result: { status: "healthy" | "degraded" | "down"; latencyMs?: number };

    if (config.channel === "email" && config.providerType === "sendgrid") {
      const adapter = new SendGridEmailProviderAdapter({
        apiKey: credentials["apiKey"] as string,
        defaultFromEmail: endpointConfig["fromEmail"] as string,
        defaultFromName: (endpointConfig["fromName"] as string) ?? "",
      });
      result = await adapter.healthCheck();
    } else if (config.channel === "email" && config.providerType === "smtp") {
      const adapter = new SmtpEmailProviderAdapter({
        host:     endpointConfig["host"] as string,
        port:     Number(endpointConfig["port"]),
        secure:   (endpointConfig["secure"] as boolean) ?? false,
        username: credentials["username"] as string,
        password: credentials["password"] as string,
        fromEmail: endpointConfig["fromEmail"] as string,
        fromName:  endpointConfig["fromName"] as string | undefined,
      });
      result = await adapter.healthCheck();
    } else if (config.channel === "sms" && config.providerType === "twilio") {
      const adapter = new TwilioSmsProviderAdapter({
        accountSid:        credentials["accountSid"] as string,
        authToken:         credentials["authToken"] as string,
        defaultFromNumber: endpointConfig["fromNumber"] as string,
      });
      result = await adapter.healthCheck();
    } else {
      return { success: false, message: `Unsupported provider for test: ${config.providerType}` };
    }

    const success = result.status !== "down";
    await repo.update(id, { healthStatus: result.status });

    return {
      success,
      latencyMs: result.latencyMs,
      message: success ? `Provider health check passed (${result.status})` : "Provider health check failed",
    };
  } catch (err) {
    logger.error("Provider test failed with exception", { configId: id, error: String(err) });
    return { success: false, message: `Test failed: ${String(err).slice(0, 200)}` };
  }
}

export async function activateTenantProviderConfig(
  id: string,
  tenantId: string | undefined
): Promise<SafeProviderConfigView> {
  const config = await repo.findByIdAndTenant(id, tenantId);
  if (!config) throw Object.assign(new Error("Provider config not found"), { statusCode: 404 });

  if (config.validationStatus !== "valid") {
    throw Object.assign(
      new Error("Cannot activate a provider config that has not been validated. Call /validate first."),
      { statusCode: 409 }
    );
  }

  await repo.update(id, { isActive: true });

  await auditClient.publishEvent({
    eventType: "tenant_provider_config.activated",
    tenantId,
    metadata: { configId: id, providerType: config.providerType, channel: config.channel },
  });

  return safeView((await repo.findByIdAndTenant(id, tenantId))!);
}

// For internal use by routing service — returns decrypted credentials
export async function deleteTenantProviderConfig(
  configId: string,
  tenantId: string | undefined
): Promise<void> {
  const config = await repo.findByIdAndTenant(configId, tenantId);
  if (!config) {
    const err = new Error("Provider config not found") as Error & { statusCode: number };
    err.statusCode = 404;
    throw err;
  }
  const deleted = await repo.deleteById(configId, tenantId);
  if (!deleted) {
    const err = new Error("Failed to delete provider config") as Error & { statusCode: number };
    err.statusCode = 500;
    throw err;
  }
  await auditClient.publishEvent({
    eventType: "tenant_provider_config.deleted",
    metadata: { configId, providerType: config.providerType },
  });
}

export async function resolveProviderCredentials(
  configId: string
): Promise<{ config: TenantProviderConfig; credentials: Record<string, unknown>; endpointConfig: Record<string, unknown> } | null> {
  const config = await repo.findById(configId);
  if (!config || !config.isActive || config.status !== "active") return null;

  const endpointConfig: Record<string, unknown> = config.endpointConfigJson
    ? (JSON.parse(config.endpointConfigJson) as Record<string, unknown>)
    : {};

  let credentials: Record<string, unknown> = {};
  if (config.credentialReference) {
    try {
      credentials = JSON.parse(decrypt(config.credentialReference)) as Record<string, unknown>;
    } catch (err) {
      logger.error("Failed to decrypt credentials for routing", { configId, error: String(err) });
      return null;
    }
  }

  return { config, credentials, endpointConfig };
}
