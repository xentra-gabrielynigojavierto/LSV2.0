import * as crypto from "crypto";
import { logger } from "../../../shared/logger";

interface TwilioVerifierConfig {
  enabled: boolean;
  authToken: string;
  nodeEnv: string;
}

export interface VerificationResult {
  verified: boolean;
  skipped: boolean;
  reason?: string;
}

/**
 * Verifies Twilio request signatures using HMAC-SHA1.
 *
 * Algorithm:
 *   1. Take the full URL of the request
 *   2. If POST: append sorted key=value pairs of the POST parameters
 *   3. Sign with HMAC-SHA1 using the Twilio auth token
 *   4. Compare base64 result with X-Twilio-Signature header
 */
export class TwilioWebhookVerifier {
  private config: TwilioVerifierConfig;

  constructor(config: TwilioVerifierConfig) {
    this.config = config;
  }

  verify(
    requestUrl: string,
    params: Record<string, string>,
    signature: string | undefined
  ): VerificationResult {
    const { enabled, authToken, nodeEnv } = this.config;

    if (!enabled) {
      if (nodeEnv === "production") {
        logger.warn(
          "Twilio webhook verification is disabled in production — requests are accepted without signature check"
        );
      }
      return { verified: false, skipped: true, reason: "verification_disabled" };
    }

    if (!authToken) {
      logger.error("Twilio webhook verification is enabled but no auth token is configured");
      return { verified: false, skipped: false, reason: "missing_auth_token" };
    }

    if (!signature) {
      logger.warn("Twilio webhook request missing X-Twilio-Signature header");
      return { verified: false, skipped: false, reason: "missing_signature" };
    }

    try {
      // Build the string to sign: URL + sorted params concatenated
      const sortedParams = Object.keys(params)
        .sort()
        .reduce((str, key) => str + key + (params[key] ?? ""), requestUrl);

      const expectedSig = crypto
        .createHmac("sha1", authToken)
        .update(sortedParams, "utf8")
        .digest("base64");

      const valid = crypto.timingSafeEqual(
        Buffer.from(signature, "base64"),
        Buffer.from(expectedSig, "base64")
      );

      if (!valid) {
        logger.warn("Twilio webhook signature verification failed");
        return { verified: false, skipped: false, reason: "invalid_signature" };
      }

      return { verified: true, skipped: false };
    } catch (err) {
      logger.error("Twilio webhook verification threw an error", { error: String(err) });
      return { verified: false, skipped: false, reason: `verification_error: ${String(err)}` };
    }
  }
}
