import * as https from "https";
import { SmsProviderAdapter, SmsSendPayload, SmsSendResult } from "../interfaces/sms-provider.interface";
import { FailureCategory } from "../../../types";
import { logger } from "../../../shared/logger";

interface TwilioConfig {
  accountSid: string;
  authToken: string;
  defaultFromNumber: string;
}

function classifyTwilioError(statusCode: number, body: string): FailureCategory {
  if (statusCode === 401 || statusCode === 403) return "auth_config_failure";
  if (statusCode === 400) {
    const lower = body.toLowerCase();
    if (lower.includes("21211") || lower.includes("21614") || lower.includes("invalid")) {
      return "invalid_recipient";
    }
    return "non_retryable_failure";
  }
  if (statusCode === 429 || statusCode >= 500) return "retryable_provider_failure";
  return "non_retryable_failure";
}

function httpsPost(
  url: string,
  headers: Record<string, string>,
  formBody: string
): Promise<{ statusCode: number; body: string }> {
  return new Promise((resolve, reject) => {
    const parsed = new URL(url);
    const options = {
      hostname: parsed.hostname,
      path: parsed.pathname + parsed.search,
      method: "POST",
      headers: {
        ...headers,
        "Content-Type": "application/x-www-form-urlencoded",
        "Content-Length": Buffer.byteLength(formBody),
      },
    };

    const req = https.request(options, (res) => {
      let data = "";
      res.on("data", (chunk) => { data += chunk; });
      res.on("end", () => resolve({ statusCode: res.statusCode ?? 0, body: data }));
    });

    req.on("error", reject);
    req.setTimeout(10000, () => { req.destroy(new Error("Request timed out")); });
    req.write(formBody);
    req.end();
  });
}

function httpsGet(
  url: string,
  headers: Record<string, string>
): Promise<{ statusCode: number; body: string }> {
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

function basicAuth(accountSid: string, authToken: string): string {
  return "Basic " + Buffer.from(`${accountSid}:${authToken}`).toString("base64");
}

export class TwilioSmsProviderAdapter implements SmsProviderAdapter {
  readonly providerType = "twilio";
  private config: TwilioConfig;

  constructor(config: TwilioConfig) {
    this.config = config;
  }

  async validateConfig(): Promise<boolean> {
    const { accountSid, authToken, defaultFromNumber } = this.config;
    if (!accountSid || !authToken || !defaultFromNumber) {
      logger.warn("Twilio config is incomplete", {
        hasAccountSid: !!accountSid,
        hasAuthToken: !!authToken,
        hasFromNumber: !!defaultFromNumber,
      });
      return false;
    }
    return true;
  }

  async send(payload: SmsSendPayload): Promise<SmsSendResult> {
    const valid = await this.validateConfig();
    if (!valid) {
      return {
        success: false,
        failure: {
          category: "auth_config_failure",
          message: "Twilio is not configured — TWILIO_ACCOUNT_SID, TWILIO_AUTH_TOKEN, or TWILIO_DEFAULT_FROM_NUMBER is missing",
          retryable: false,
        },
      };
    }

    const { accountSid, authToken, defaultFromNumber } = this.config;
    const formBody = new URLSearchParams({
      To: payload.to,
      From: payload.from ?? defaultFromNumber,
      Body: payload.body,
    }).toString();

    const url = `https://api.twilio.com/2010-04-01/Accounts/${accountSid}/Messages.json`;

    try {
      const result = await httpsPost(url, { Authorization: basicAuth(accountSid, authToken) }, formBody);

      if (result.statusCode === 201) {
        let sid: string | undefined;
        try {
          const parsed = JSON.parse(result.body) as { sid?: string };
          sid = parsed.sid;
        } catch { /* ignore */ }

        logger.info("Twilio: SMS sent successfully", { to: payload.to, sid });
        return { success: true, providerMessageId: sid };
      }

      const category = classifyTwilioError(result.statusCode, result.body);
      logger.warn("Twilio: send failed", { statusCode: result.statusCode, category });

      return {
        success: false,
        failure: {
          category,
          providerCode: String(result.statusCode),
          message: result.body.slice(0, 500),
          retryable: category === "retryable_provider_failure" || category === "provider_unavailable",
        },
      };
    } catch (err) {
      const message = String(err);
      const isTimeout = message.includes("timed out");
      logger.error("Twilio: network error during send", { error: message });
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

  async healthCheck(): Promise<{ status: "healthy" | "degraded" | "down"; latencyMs?: number }> {
    const { accountSid, authToken } = this.config;
    if (!accountSid || !authToken) return { status: "down" };

    const url = `https://api.twilio.com/2010-04-01/Accounts/${accountSid}.json`;
    const start = Date.now();
    try {
      const result = await httpsGet(url, { Authorization: basicAuth(accountSid, authToken) });
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
