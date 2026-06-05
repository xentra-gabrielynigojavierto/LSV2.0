import * as https from "https";
import { EmailProviderAdapter, EmailSendPayload, EmailSendResult } from "../interfaces/email-provider.interface";
import { FailureCategory } from "../../../types";
import { logger } from "../../../shared/logger";

interface SendGridConfig {
  apiKey: string;
  defaultFromEmail: string;
  defaultFromName: string;
}

function classifySendGridError(statusCode: number, body: string): FailureCategory {
  if (statusCode === 401 || statusCode === 403) return "auth_config_failure";
  if (statusCode === 400) {
    const lower = body.toLowerCase();
    if (lower.includes("invalid") && (lower.includes("email") || lower.includes("recipient"))) {
      return "invalid_recipient";
    }
    return "non_retryable_failure";
  }
  if (statusCode === 413 || statusCode === 422) return "non_retryable_failure";
  if (statusCode === 429 || statusCode >= 500) return "retryable_provider_failure";
  return "non_retryable_failure";
}

function httpsPost(url: string, headers: Record<string, string>, body: string): Promise<{ statusCode: number; body: string }> {
  return new Promise((resolve, reject) => {
    const parsed = new URL(url);
    const options = {
      hostname: parsed.hostname,
      path: parsed.pathname + parsed.search,
      method: "POST",
      headers: { ...headers, "Content-Length": Buffer.byteLength(body) },
    };

    const req = https.request(options, (res) => {
      let data = "";
      res.on("data", (chunk) => { data += chunk; });
      res.on("end", () => resolve({ statusCode: res.statusCode ?? 0, body: data }));
    });

    req.on("error", reject);
    req.setTimeout(10000, () => { req.destroy(new Error("Request timed out")); });
    req.write(body);
    req.end();
  });
}

function httpsGet(url: string, headers: Record<string, string>): Promise<{ statusCode: number; body: string }> {
  return new Promise((resolve, reject) => {
    const parsed = new URL(url);
    const options = {
      hostname: parsed.hostname,
      path: parsed.pathname + parsed.search,
      method: "GET",
      headers,
    };

    const req = https.request(options, (res) => {
      let data = "";
      res.on("data", (chunk) => { data += chunk; });
      res.on("end", () => resolve({ statusCode: res.statusCode ?? 0, body: data }));
    });

    req.on("error", reject);
    req.setTimeout(8000, () => { req.destroy(new Error("Health check timed out")); });
    req.end();
  });
}

export class SendGridEmailProviderAdapter implements EmailProviderAdapter {
  readonly providerType = "sendgrid";
  private config: SendGridConfig;

  constructor(config: SendGridConfig) {
    this.config = config;
  }

  async validateConfig(): Promise<boolean> {
    const { apiKey, defaultFromEmail } = this.config;
    if (!apiKey || !defaultFromEmail) {
      logger.warn("SendGrid config is incomplete", { hasApiKey: !!apiKey, hasFromEmail: !!defaultFromEmail });
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
          message: "SendGrid is not configured — SENDGRID_API_KEY or SENDGRID_DEFAULT_FROM_EMAIL is missing",
          retryable: false,
        },
      };
    }

    const body = JSON.stringify({
      personalizations: [{ to: [{ email: payload.to }] }],
      from: {
        email: payload.from || this.config.defaultFromEmail,
        name: this.config.defaultFromName || undefined,
      },
      subject: payload.subject,
      content: [
        { type: "text/plain", value: payload.body },
        ...(payload.html ? [{ type: "text/html", value: payload.html }] : []),
      ],
    });

    try {
      const result = await httpsPost(
        "https://api.sendgrid.com/v3/mail/send",
        {
          Authorization: `Bearer ${this.config.apiKey}`,
          "Content-Type": "application/json",
        },
        body
      );

      if (result.statusCode === 202) {
        logger.info("SendGrid: email sent successfully", { to: payload.to });
        return { success: true };
      }

      const category = classifySendGridError(result.statusCode, result.body);
      logger.warn("SendGrid: send failed", { statusCode: result.statusCode, category });

      return {
        success: false,
        failure: {
          category,
          providerCode: String(result.statusCode),
          message: result.body.slice(0, 500),
          retryable: category === "retryable_provider_failure",
        },
      };
    } catch (err) {
      const message = String(err);
      const isTimeout = message.includes("timed out");
      logger.error("SendGrid: network error during send", { error: message });
      return {
        success: false,
        failure: {
          category: isTimeout ? "provider_unavailable" : "retryable_provider_failure",
          message,
          retryable: true,
        },
      };
    }
  }

  /**
   * Queries the SendGrid Email Activity API for the latest delivery status of a recently
   * sent message.  Returns null when the message hasn't appeared yet or when the API
   * isn't accessible (e.g. plan doesn't include Email Activity).
   */
  async queryMessageStatus(
    toEmail: string,
  ): Promise<"delivered" | "not_delivered" | "blocked" | "bounced" | "deferred" | "spam_report" | "processing" | null> {
    if (!this.config.apiKey) return null;
    try {
      const query = encodeURIComponent(`to_email="${toEmail}"`);
      const result = await httpsGet(
        `https://api.sendgrid.com/v3/messages?query=${query}&limit=1&orderby=last_event_time+desc`,
        { Authorization: `Bearer ${this.config.apiKey}` },
      );
      if (result.statusCode !== 200) {
        logger.warn("SendGrid Messages API returned non-200", { statusCode: result.statusCode, body: result.body.slice(0, 200) });
        return null;
      }
      const body = JSON.parse(result.body) as { messages?: { status: string }[] };
      const first = body.messages?.[0];
      if (!first) return null;
      const s = first.status as string;
      const validStatuses = ["delivered", "not_delivered", "blocked", "bounced", "deferred", "spam_report", "processing"];
      if (validStatuses.includes(s)) {
        return s as "delivered" | "not_delivered" | "blocked" | "bounced" | "deferred" | "spam_report" | "processing";
      }
      return null;
    } catch (err) {
      logger.warn("SendGrid queryMessageStatus error", { error: String(err) });
      return null;
    }
  }

  async healthCheck(): Promise<{ status: "healthy" | "degraded" | "down"; latencyMs?: number }> {
    if (!this.config.apiKey) {
      return { status: "down" };
    }

    const start = Date.now();
    try {
      const result = await httpsGet("https://api.sendgrid.com/v3/scopes", {
        Authorization: `Bearer ${this.config.apiKey}`,
      });
      const latencyMs = Date.now() - start;

      if (result.statusCode === 200) return { status: "healthy", latencyMs };
      if (result.statusCode === 401 || result.statusCode === 403) return { status: "down", latencyMs };
      if (result.statusCode >= 500) return { status: "degraded", latencyMs };
      return { status: "healthy", latencyMs };
    } catch {
      return { status: "down", latencyMs: Date.now() - start };
    }
  }
}
