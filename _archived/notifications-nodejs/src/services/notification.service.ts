import { NotificationRepository } from "../repositories/notification.repository";
import { NotificationAttemptRepository } from "../repositories/notification-attempt.repository";
import { ProviderRoutingService, isFailoverEligible, ResolvedProvider } from "./provider-routing.service";
import { TemplateResolutionService } from "./template-resolution.service";
import { templateRenderingService } from "./template-rendering.service";
import { brandingResolutionService } from "./branding-resolution.service";
import { resolveProviderCredentials } from "./tenant-provider-config.service";
import { providerRegistry } from "../integrations/providers/registry/provider.registry";
import { SendGridEmailProviderAdapter } from "../integrations/providers/adapters/sendgrid.adapter";
import { SmtpEmailProviderAdapter } from "../integrations/providers/adapters/smtp.adapter";
import { TwilioSmsProviderAdapter } from "../integrations/providers/adapters/twilio.adapter";
import { auditClient } from "../integrations/audit/audit.client";
import { meter } from "./usage-metering.service";
import { checkRequestAllowed, checkAttemptAllowed } from "./usage-evaluation.service";
import { evaluateContactEnforcement } from "./contact-enforcement.service";
import { normalizeContactValue } from "../shared/contact-normalizer";
import { logger } from "../shared/logger";
import { NotificationChannel, FailureCategory } from "../types";
import { EmailSendPayload, EmailProviderAdapter } from "../integrations/providers/interfaces/email-provider.interface";
import { SmsSendPayload, SmsProviderAdapter } from "../integrations/providers/interfaces/sms-provider.interface";

export interface EmailRecipient { email: string }
export interface SmsRecipient { phoneNumber: string }
export interface EmailMessage { subject: string; body: string; html?: string }
export interface SmsMessage { body: string }

export interface SubmitNotificationInput {
  tenantId: string;
  channel: NotificationChannel;
  recipient: EmailRecipient | SmsRecipient;
  message?: EmailMessage | SmsMessage;
  metadata?: Record<string, unknown>;
  idempotencyKey?: string;
  // Template mode
  templateKey?: string;
  templateData?: Record<string, unknown>;
  productType?: import("../types").ProductType;
  // NOTIF-007: Override suppression
  sendOptions?: {
    overrideSuppression?: boolean;
    overrideReason?: string;
  };
}

export interface SubmitNotificationResult {
  notificationId: string;
  status: string;
  providerUsed?: string;
  providerMessageId?: string;
  idempotent?: boolean;
}

export interface ListNotificationsInput {
  tenantId?: string;
  channel?: NotificationChannel;
  status?: string;
  limit?: number;
  offset?: number;
}

export class NotificationService {
  private notificationRepo: NotificationRepository;
  private attemptRepo: NotificationAttemptRepository;
  private routingService: ProviderRoutingService;
  private templateResolutionSvc: TemplateResolutionService;

  constructor(routingService: ProviderRoutingService) {
    this.notificationRepo = new NotificationRepository();
    this.attemptRepo = new NotificationAttemptRepository();
    this.routingService = routingService;
    this.templateResolutionSvc = new TemplateResolutionService();
  }

  async submit(input: SubmitNotificationInput): Promise<SubmitNotificationResult> {
    const { tenantId, channel, recipient, metadata, idempotencyKey } = input;
    const isTemplateMode = !!input.templateKey;
    const sendOptions = input.sendOptions ?? {};

    // ── NOTIF-006: Enforcement check (rate limit + quota) ─────────────────────
    // Must happen before any persistence or provider interaction
    const enforcement = await checkRequestAllowed(tenantId, channel);
    if (!enforcement.allowed) {
      // Meter the rejected request before throwing — fire-and-forget, never throws
      void meter({
        tenantId,
        usageUnit: "api_notification_request_rejected",
        channel,
        metadata: { reason: enforcement.reason, code: enforcement.code },
      });
      await auditClient.publishEvent({
        eventType: "request.rejected_by_policy",
        tenantId,
        channel,
        metadata: { reason: enforcement.reason, code: enforcement.code },
      });
      throw Object.assign(
        new Error(enforcement.reason ?? "Request rejected by rate limit or quota policy"),
        { statusCode: 429, code: enforcement.code ?? "RATE_LIMIT_EXCEEDED" }
      );
    }

    // Resolve message content from template or raw
    let message: EmailMessage | SmsMessage;
    let templateFields: {
      templateId?: string;
      templateVersionId?: string;
      templateKey?: string;
      renderedSubject?: string;
      renderedBody?: string;
      renderedText?: string;
    } = {};

    if (isTemplateMode) {
      const { templateKey, templateData = {} } = input;

      const resolved = input.productType
        ? await this.templateResolutionSvc.resolveByProduct(tenantId, templateKey!, channel, input.productType)
        : await this.templateResolutionSvc.resolve(tenantId, templateKey!, channel);
      if (!resolved) {
        await auditClient.publishEvent({
          eventType: "template.resolution.failed",
          tenantId,
          channel,
          metadata: { templateKey, channel },
        });
        throw Object.assign(
          new Error(`Template '${templateKey}' not found or has no published version for channel '${channel}'`),
          { statusCode: 404, code: "TEMPLATE_NOT_FOUND" }
        );
      }

      let renderResult: { result: import("./template-rendering.service").RenderResult; errors: string[] };
      if (resolved.template.isBrandable && resolved.template.productType) {
        const branding = await brandingResolutionService.resolve(tenantId, resolved.template.productType);
        const brandingTokens = brandingResolutionService.buildBrandingTokens(branding);
        renderResult = templateRenderingService.renderBranded(resolved.version, templateData, brandingTokens);
      } else {
        renderResult = templateRenderingService.render(resolved.version, templateData);
      }
      const { result: rendered, errors } = renderResult;
      if (errors.length > 0) {
        throw Object.assign(
          new Error(`Template rendering failed: ${errors.join("; ")}`),
          { statusCode: 422, code: "TEMPLATE_RENDER_FAILED" }
        );
      }

      // Build the message from rendered content
      if (channel === "email") {
        message = {
          subject: rendered.subject ?? "",
          body: rendered.body,
          html: rendered.body,
        };
      } else {
        message = { body: rendered.body };
      }

      templateFields = {
        templateId: resolved.template.id,
        templateVersionId: resolved.version.id,
        templateKey: templateKey!,
        renderedSubject: rendered.subject ?? undefined,
        renderedBody: rendered.body,
        renderedText: rendered.text ?? undefined,
      };

      // ── NOTIF-006: Meter template render ─────────────────────────────────────
      void meter({
        tenantId,
        usageUnit: "template_render",
        channel,
        metadata: { templateKey, templateId: resolved.template.id, versionId: resolved.version.id },
      });

      await auditClient.publishEvent({
        eventType: "template.notification.submitted",
        tenantId,
        channel,
        metadata: {
          templateKey,
          templateId: resolved.template.id,
          versionId: resolved.version.id,
        },
      });
    } else {
      if (!input.message) {
        throw Object.assign(new Error("message is required in raw mode"), { statusCode: 400, code: "VALIDATION_ERROR" });
      }
      message = input.message;
    }

    // Idempotency check
    if (idempotencyKey) {
      const existing = await this.notificationRepo.findByIdempotencyKey(tenantId, idempotencyKey);
      if (existing) {
        logger.info("Idempotent notification — returning existing record", {
          tenantId,
          idempotencyKey,
          notificationId: existing.id,
        });
        return {
          notificationId: existing.id,
          status: existing.status,
          providerUsed: existing.providerUsed ?? undefined,
          idempotent: true,
        };
      }
    }

    // Persist notification
    const notification = await this.notificationRepo.create({
      tenantId,
      channel,
      recipientJson: JSON.stringify(recipient),
      messageJson: JSON.stringify(isTemplateMode ? (input.templateData ?? {}) : message),
      metadataJson: metadata ? JSON.stringify(metadata) : null,
      idempotencyKey: idempotencyKey ?? null,
      ...templateFields,
    });

    await auditClient.publishEvent({
      eventType: "notification.accepted",
      tenantId,
      channel,
      metadata: { notificationId: notification.id },
    });

    // ── NOTIF-006: Meter accepted request ─────────────────────────────────────
    void meter({
      tenantId,
      usageUnit: "api_notification_request",
      channel,
      notificationId: notification.id,
    });

    // ── NOTIF-007: Contact enforcement check ──────────────────────────────────
    const contactValue = channel === "email"
      ? (recipient as EmailRecipient).email
      : (recipient as SmsRecipient).phoneNumber;

    const contactEnforcement = await evaluateContactEnforcement({
      tenantId,
      channel,
      contactValue,
      overrideSuppression: sendOptions.overrideSuppression,
      overrideReason: sendOptions.overrideReason,
    });

    if (!contactEnforcement.allowed) {
      // Record as blocked — no provider attempt will be created
      await this.notificationRepo.update(notification.id, {
        status: "blocked",
        blockedByPolicy: true,
        blockedReasonCode: contactEnforcement.reasonCode ?? "suppression_enforced",
        lastErrorMessage: contactEnforcement.reasonMessage,
      });

      void meter({
        tenantId,
        usageUnit: "suppressed_notification_request_rejected",
        channel,
        notificationId: notification.id,
        metadata: {
          reasonCode: contactEnforcement.reasonCode,
          matchedSuppressionId: contactEnforcement.matchedSuppressionId,
          matchedHealthStatus: contactEnforcement.matchedHealthStatus,
        },
      });

      await auditClient.publishEvent({
        eventType: "notification.blocked_by_suppression",
        tenantId,
        channel,
        metadata: {
          notificationId: notification.id,
          reasonCode: contactEnforcement.reasonCode,
          matchedSuppressionId: contactEnforcement.matchedSuppressionId,
          matchedHealthStatus: contactEnforcement.matchedHealthStatus,
          overrideAttempted: sendOptions.overrideSuppression ?? false,
          overrideAllowed: contactEnforcement.overrideAllowed,
        },
      });

      throw Object.assign(
        new Error(contactEnforcement.reasonMessage),
        { statusCode: 451, code: contactEnforcement.reasonCode ?? "CONTACT_SUPPRESSED" }
      );
    }

    // If override was used (allowed send despite suppression)
    if (contactEnforcement.overrideUsed) {
      await this.notificationRepo.update(notification.id, {
        overrideUsed: true,
      });
      await auditClient.publishEvent({
        eventType: "notification.override_used",
        tenantId,
        channel,
        metadata: {
          notificationId: notification.id,
          overrideReason: sendOptions.overrideReason,
          matchedSuppressionId: contactEnforcement.matchedSuppressionId,
          reasonCode: contactEnforcement.reasonCode,
        },
      });
    }

    // Mark as processing
    await this.notificationRepo.update(notification.id, { status: "processing" });

    // Resolve provider
    const resolved = await this.routingService.resolveProvider(tenantId, channel);
    if (!resolved) {
      const msg = `No eligible provider available for channel: ${channel}`;
      logger.error(msg, { tenantId, channel });
      await this.notificationRepo.update(notification.id, {
        status: "failed",
        failureCategory: "provider_unavailable",
        lastErrorMessage: msg,
      });
      await auditClient.publishEvent({
        eventType: "notification.permanently_failed",
        tenantId,
        channel,
        metadata: { notificationId: notification.id, reason: "no_eligible_provider" },
      });
      throw Object.assign(new Error(msg), { statusCode: 503, code: "NO_ELIGIBLE_PROVIDER" });
    }

    // Record routing decision on notification
    await this.notificationRepo.update(notification.id, {
      providerOwnershipMode: resolved.ownershipMode,
      providerConfigId: resolved.providerConfigId ?? null,
      platformFallbackUsed: resolved.platformFallbackUsed,
    });

    // Execute primary send
    const result = await this.executeSend({
      notificationId: notification.id,
      tenantId,
      channel,
      recipient,
      message,
      providerType: resolved.providerType,
      attemptNumber: 1,
      failoverTriggered: false,
      ownershipMode: resolved.ownershipMode,
      providerConfigId: resolved.providerConfigId,
      platformFallbackUsed: resolved.platformFallbackUsed,
    });

    if (result.success) {
      await this.notificationRepo.update(notification.id, {
        status: "sent",
        providerUsed: resolved.providerType,
      });
      const healthTenantId = resolved.ownershipMode === "tenant_managed" ? tenantId : null;
      await this.routingService.recordProviderHealth(resolved.providerType, channel, "healthy", false, healthTenantId);
      return {
        notificationId: notification.id,
        status: "sent",
        providerUsed: resolved.providerType,
        providerMessageId: result.providerMessageId,
      };
    }

    // Primary send failed
    const failureCategory = result.failureCategory ?? "retryable_provider_failure";
    logger.warn("Primary send failed", {
      notificationId: notification.id,
      provider: resolved.providerType,
      category: failureCategory,
    });

    const primaryHealthTenantId = resolved.ownershipMode === "tenant_managed" ? tenantId : null;
    await this.routingService.recordProviderHealth(resolved.providerType, channel, "degraded", true, primaryHealthTenantId);

    await auditClient.publishEvent({
      eventType: "provider.send_failed",
      tenantId,
      channel,
      provider: resolved.providerType,
      metadata: {
        notificationId: notification.id,
        category: failureCategory,
        message: result.errorMessage,
      },
    });

    // Decide whether to failover
    if (!isFailoverEligible(failureCategory)) {
      await this.notificationRepo.update(notification.id, {
        status: "failed",
        providerUsed: resolved.providerType,
        failureCategory,
        lastErrorMessage: result.errorMessage ?? null,
      });
      await auditClient.publishEvent({
        eventType: "notification.permanently_failed",
        tenantId,
        channel,
        metadata: { notificationId: notification.id, reason: "non_retryable", category: failureCategory },
      });
      throw Object.assign(
        new Error(result.errorMessage ?? "Notification send failed (non-retryable)"),
        { statusCode: 422, code: "SEND_FAILED_NON_RETRYABLE" }
      );
    }

    // Attempt failover
    const failoverProvider = await this.routingService.getNextProvider(tenantId, channel, [
      resolved.providerType,
    ]);

    if (!failoverProvider) {
      await this.notificationRepo.update(notification.id, {
        status: "failed",
        providerUsed: resolved.providerType,
        failureCategory,
        lastErrorMessage: "Primary failed and no failover provider available",
      });
      await auditClient.publishEvent({
        eventType: "notification.permanently_failed",
        tenantId,
        channel,
        metadata: { notificationId: notification.id, reason: "no_failover_provider" },
      });
      throw Object.assign(new Error("Send failed and no failover provider is available"), {
        statusCode: 503,
        code: "FAILOVER_UNAVAILABLE",
      });
    }

    await auditClient.publishEvent({
      eventType: "failover.triggered",
      tenantId,
      channel,
      provider: failoverProvider.providerType,
      metadata: {
        notificationId: notification.id,
        fromProvider: resolved.providerType,
        toProvider: failoverProvider.providerType,
      },
    });

    // ── NOTIF-006: Meter provider failover attempt ─────────────────────────────
    void meter({
      tenantId,
      usageUnit: "provider_failover_attempt",
      channel,
      notificationId: notification.id,
      provider: failoverProvider.providerType,
      providerOwnershipMode: failoverProvider.ownershipMode,
      providerConfigId: failoverProvider.providerConfigId ?? null,
      metadata: { fromProvider: resolved.providerType, toProvider: failoverProvider.providerType },
    });

    const failoverResult = await this.executeSend({
      notificationId: notification.id,
      tenantId,
      channel,
      recipient,
      message,
      providerType: failoverProvider.providerType,
      attemptNumber: 2,
      failoverTriggered: true,
      ownershipMode: failoverProvider.ownershipMode,
      providerConfigId: failoverProvider.providerConfigId,
      platformFallbackUsed: failoverProvider.platformFallbackUsed,
    });

    if (failoverResult.success) {
      await this.notificationRepo.update(notification.id, {
        status: "sent",
        providerUsed: failoverProvider.providerType,
        platformFallbackUsed: failoverProvider.platformFallbackUsed,
      });
      const failoverHealthTenantId = failoverProvider.ownershipMode === "tenant_managed" ? tenantId : null;
      await this.routingService.recordProviderHealth(failoverProvider.providerType, channel, "healthy", false, failoverHealthTenantId);
      return {
        notificationId: notification.id,
        status: "sent",
        providerUsed: failoverProvider.providerType,
        providerMessageId: failoverResult.providerMessageId,
      };
    }

    // Both providers failed
    const finalCategory = failoverResult.failureCategory ?? failureCategory;
    await this.notificationRepo.update(notification.id, {
      status: "failed",
      providerUsed: failoverProvider.providerType,
      failureCategory: finalCategory,
      lastErrorMessage: failoverResult.errorMessage ?? "All providers exhausted",
    });
    const failoverFinalTenantId = failoverProvider.ownershipMode === "tenant_managed" ? tenantId : null;
    await this.routingService.recordProviderHealth(failoverProvider.providerType, channel, "degraded", true, failoverFinalTenantId);
    await auditClient.publishEvent({
      eventType: "notification.permanently_failed",
      tenantId,
      channel,
      metadata: {
        notificationId: notification.id,
        reason: "all_providers_exhausted",
        category: finalCategory,
      },
    });

    throw Object.assign(new Error("All providers exhausted — notification permanently failed"), {
      statusCode: 503,
      code: "ALL_PROVIDERS_FAILED",
    });
  }

  private async executeSend(params: {
    notificationId: string;
    tenantId: string;
    channel: NotificationChannel;
    recipient: EmailRecipient | SmsRecipient;
    message: EmailMessage | SmsMessage;
    providerType: string;
    attemptNumber: number;
    failoverTriggered: boolean;
    ownershipMode?: "platform_managed" | "tenant_managed";
    providerConfigId?: string | null;
    platformFallbackUsed?: boolean;
  }): Promise<{ success: boolean; providerMessageId?: string; failureCategory?: FailureCategory; errorMessage?: string }> {
    const { notificationId, tenantId, channel, recipient, message, providerType, attemptNumber, failoverTriggered } = params;
    const ownershipMode = params.ownershipMode ?? "platform_managed";
    const providerConfigId = params.providerConfigId ?? null;
    const platformFallbackUsed = params.platformFallbackUsed ?? false;

    const attempt = await this.attemptRepo.create({
      tenantId,
      notificationId,
      attemptNumber,
      provider: providerType,
      failoverTriggered,
      providerOwnershipMode: ownershipMode,
      providerConfigId,
      platformFallbackUsed,
    });

    await auditClient.publishEvent({
      eventType: "provider.send_attempted",
      tenantId,
      channel,
      provider: providerType,
      metadata: { notificationId, attemptId: attempt.id, attemptNumber },
    });

    if (channel === "email") {
      let adapter: EmailProviderAdapter | null = null;

      if (ownershipMode === "tenant_managed" && providerConfigId) {
        const resolved = await resolveProviderCredentials(providerConfigId);
        if (!resolved) {
          await this.attemptRepo.complete(attempt.id, {
            status: "failed",
            failureCategory: "auth_config_failure",
            errorMessage: `Tenant provider config ${providerConfigId} could not be resolved (inactive or decryption failed)`,
          });
          return { success: false, failureCategory: "auth_config_failure", errorMessage: "Tenant provider credentials could not be resolved" };
        }

        const { config, credentials, endpointConfig } = resolved;

        if (config.providerType === "sendgrid") {
          adapter = new SendGridEmailProviderAdapter({
            apiKey: credentials["apiKey"] as string,
            defaultFromEmail: endpointConfig["fromEmail"] as string,
            defaultFromName: (endpointConfig["fromName"] as string) ?? "",
          });
        } else if (config.providerType === "smtp") {
          adapter = new SmtpEmailProviderAdapter({
            host: endpointConfig["host"] as string,
            port: Number(endpointConfig["port"]),
            secure: (endpointConfig["secure"] as boolean) ?? false,
            username: credentials["username"] as string,
            password: credentials["password"] as string,
            fromEmail: endpointConfig["fromEmail"] as string,
            fromName: endpointConfig["fromName"] as string | undefined,
          });
        }
      } else {
        adapter = providerRegistry.getEmailProvider(providerType) ?? null;
      }

      if (!adapter) {
        await this.attemptRepo.complete(attempt.id, {
          status: "failed",
          failureCategory: "provider_unavailable",
          errorMessage: `Email provider "${providerType}" is not available`,
        });
        return { success: false, failureCategory: "provider_unavailable", errorMessage: `Email provider not available: ${providerType}` };
      }

      const emailRecipient = recipient as EmailRecipient;
      const emailMessage = message as EmailMessage;

      const payload: EmailSendPayload = {
        to: emailRecipient.email,
        from: "",
        subject: emailMessage.subject,
        body: emailMessage.body,
        html: emailMessage.html,
      };

      // ── NOTIF-006: Meter email attempt ──────────────────────────────────────
      void meter({
        tenantId,
        usageUnit: "email_attempt",
        channel,
        notificationId,
        notificationAttemptId: attempt.id,
        provider: providerType,
        providerOwnershipMode: ownershipMode,
        providerConfigId,
      });

      const result = await adapter.send(payload);

      await this.attemptRepo.complete(attempt.id, {
        status: result.success ? "sent" : "failed",
        providerMessageId: result.providerMessageId ?? null,
        failureCategory: result.failure?.category ?? null,
        errorMessage: result.failure?.message ?? null,
      });

      // ── NOTIF-006: Meter email sent on success ──────────────────────────────
      if (result.success) {
        void meter({
          tenantId,
          usageUnit: "email_sent",
          channel,
          notificationId,
          notificationAttemptId: attempt.id,
          provider: providerType,
          providerOwnershipMode: ownershipMode,
          providerConfigId,
        });
      }

      return {
        success: result.success,
        providerMessageId: result.providerMessageId,
        failureCategory: result.failure?.category,
        errorMessage: result.failure?.message,
      };
    }

    if (channel === "sms") {
      let adapter: SmsProviderAdapter | null = null;

      if (ownershipMode === "tenant_managed" && providerConfigId) {
        const resolved = await resolveProviderCredentials(providerConfigId);
        if (!resolved) {
          await this.attemptRepo.complete(attempt.id, {
            status: "failed",
            failureCategory: "auth_config_failure",
            errorMessage: `Tenant provider config ${providerConfigId} could not be resolved`,
          });
          return { success: false, failureCategory: "auth_config_failure", errorMessage: "Tenant provider credentials could not be resolved" };
        }

        const { config, credentials, endpointConfig } = resolved;

        if (config.providerType === "twilio") {
          adapter = new TwilioSmsProviderAdapter({
            accountSid: credentials["accountSid"] as string,
            authToken: credentials["authToken"] as string,
            defaultFromNumber: endpointConfig["fromNumber"] as string,
          });
        }
      } else {
        adapter = providerRegistry.getSmsProvider(providerType) ?? null;
      }

      if (!adapter) {
        await this.attemptRepo.complete(attempt.id, {
          status: "failed",
          failureCategory: "provider_unavailable",
          errorMessage: `SMS provider "${providerType}" is not available`,
        });
        return { success: false, failureCategory: "provider_unavailable", errorMessage: `SMS provider not available: ${providerType}` };
      }

      const smsRecipient = recipient as SmsRecipient;
      const smsMessage = message as SmsMessage;

      const payload: SmsSendPayload = {
        to: smsRecipient.phoneNumber,
        body: smsMessage.body,
      };

      // ── NOTIF-006: Meter SMS attempt ────────────────────────────────────────
      void meter({
        tenantId,
        usageUnit: "sms_attempt",
        channel,
        notificationId,
        notificationAttemptId: attempt.id,
        provider: providerType,
        providerOwnershipMode: ownershipMode,
        providerConfigId,
      });

      const result = await adapter.send(payload);

      await this.attemptRepo.complete(attempt.id, {
        status: result.success ? "sent" : "failed",
        providerMessageId: result.providerMessageId ?? null,
        failureCategory: result.failure?.category ?? null,
        errorMessage: result.failure?.message ?? null,
      });

      // ── NOTIF-006: Meter SMS sent on success (with provider cost if available)
      if (result.success) {
        const providerUnitCost = (result as { price?: number }).price ?? null;
        void meter({
          tenantId,
          usageUnit: "sms_sent",
          channel,
          notificationId,
          notificationAttemptId: attempt.id,
          provider: providerType,
          providerOwnershipMode: ownershipMode,
          providerConfigId,
          providerUnitCost,
          providerTotalCost: providerUnitCost,
        });
      }

      return {
        success: result.success,
        providerMessageId: result.providerMessageId,
        failureCategory: result.failure?.category,
        errorMessage: result.failure?.message,
      };
    }

    await this.attemptRepo.complete(attempt.id, {
      status: "failed",
      failureCategory: "non_retryable_failure",
      errorMessage: `Unsupported channel: ${channel}`,
    });
    return { success: false, failureCategory: "non_retryable_failure", errorMessage: `Unsupported channel: ${channel}` };
  }

  async getById(id: string, tenantId: string) {
    return this.notificationRepo.findById(id, tenantId);
  }

  async list(input: ListNotificationsInput) {
    return this.notificationRepo.list({
      tenantId: input.tenantId,
      channel: input.channel,
      status: input.status as any,
      limit: input.limit,
      offset: input.offset,
    });
  }

  async getStats(tenantId?: string) {
    return this.notificationRepo.getStats(tenantId);
  }
}
