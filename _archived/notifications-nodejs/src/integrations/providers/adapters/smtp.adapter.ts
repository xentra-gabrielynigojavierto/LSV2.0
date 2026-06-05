import * as nodemailer from "nodemailer";
import { EmailProviderAdapter, EmailSendPayload, EmailSendResult } from "../interfaces/email-provider.interface";
import { FailureCategory } from "../../../types";
import { logger } from "../../../shared/logger";

export interface SmtpAdapterConfig {
  host: string;
  port: number;
  secure: boolean;
  username: string;
  password: string;
  fromEmail: string;
  fromName?: string;
}

function classifySmtpError(err: unknown): FailureCategory {
  const msg = String(err).toLowerCase();
  if (msg.includes("auth") || msg.includes("credential") || msg.includes("535") || msg.includes("534")) {
    return "auth_config_failure";
  }
  if (msg.includes("timeout") || msg.includes("econnrefused") || msg.includes("enotfound")) {
    return "provider_unavailable";
  }
  if (msg.includes("550") || msg.includes("invalid") || msg.includes("recipient")) {
    return "invalid_recipient";
  }
  return "retryable_provider_failure";
}

export class SmtpEmailProviderAdapter implements EmailProviderAdapter {
  readonly providerType = "smtp";
  private config: SmtpAdapterConfig;

  constructor(config: SmtpAdapterConfig) {
    this.config = config;
  }

  async validateConfig(): Promise<boolean> {
    const { host, port, username, password, fromEmail } = this.config;
    if (!host || !port || !username || !password || !fromEmail) {
      logger.warn("SMTP config is incomplete", {
        hasHost: !!host,
        hasPort: !!port,
        hasUsername: !!username,
        hasPassword: !!password,
        hasFromEmail: !!fromEmail,
      });
      return false;
    }
    return true;
  }

  async send(payload: EmailSendPayload): Promise<EmailSendResult> {
    const valid = await this.validateConfig();
    if (!valid) {
      return {
        success: false,
        failure: {
          category: "auth_config_failure",
          message: "SMTP config is incomplete — check host, port, username, password, and fromEmail",
          retryable: false,
        },
      };
    }

    const { host, port, secure, username, password, fromEmail, fromName } = this.config;

    const transporter = nodemailer.createTransport({
      host,
      port,
      secure,
      auth: { user: username, pass: password },
      connectionTimeout: 10000,
      greetingTimeout: 5000,
    });

    try {
      const info = await transporter.sendMail({
        from: fromName ? `"${fromName}" <${fromEmail}>` : fromEmail,
        to: payload.to,
        subject: payload.subject,
        text: payload.body,
        html: payload.html ?? payload.body,
        replyTo: payload.replyTo,
      });

      logger.info("SMTP: email sent", { messageId: info.messageId, to: payload.to });
      return { success: true, providerMessageId: info.messageId ?? undefined };
    } catch (err) {
      const message = String(err);
      const category = classifySmtpError(err);
      logger.warn("SMTP: send failed", { error: message, category });

      return {
        success: false,
        failure: {
          category,
          message: message.slice(0, 500),
          retryable: category === "retryable_provider_failure" || category === "provider_unavailable",
        },
      };
    } finally {
      transporter.close();
    }
  }

  async healthCheck(): Promise<{ status: "healthy" | "degraded" | "down"; latencyMs?: number }> {
    const valid = await this.validateConfig();
    if (!valid) return { status: "down" };

    const { host, port, secure, username, password } = this.config;
    const transporter = nodemailer.createTransport({
      host,
      port,
      secure,
      auth: { user: username, pass: password },
      connectionTimeout: 5000,
    });

    const start = Date.now();
    try {
      await transporter.verify();
      const latencyMs = Date.now() - start;
      logger.debug("SMTP health check passed", { host, port, latencyMs });
      return { status: "healthy", latencyMs };
    } catch (err) {
      const latencyMs = Date.now() - start;
      const category = classifySmtpError(err);
      logger.warn("SMTP health check failed", { error: String(err), category });

      if (category === "auth_config_failure") return { status: "down", latencyMs };
      return { status: "degraded", latencyMs };
    } finally {
      transporter.close();
    }
  }
}
