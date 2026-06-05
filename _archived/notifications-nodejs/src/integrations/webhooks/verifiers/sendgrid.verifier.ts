import * as crypto from "crypto";
import { logger } from "../../../shared/logger";

interface SendGridVerifierConfig {
  enabled: boolean;
  publicKey: string;
  nodeEnv: string;
}

export interface VerificationResult {
  verified: boolean;
  skipped: boolean;
  reason?: string;
}

/**
 * Verifies SendGrid Event Webhook signatures using ECDSA (P-256 + SHA256).
 *
 * Headers used:
 *   X-Twilio-Email-Event-Webhook-Signature — base64 ECDSA signature
 *   X-Twilio-Email-Event-Webhook-Timestamp — Unix timestamp string
 *
 * Verification payload: timestamp + rawBody (no separator)
 */
export class SendGridWebhookVerifier {
  private config: SendGridVerifierConfig;

  constructor(config: SendGridVerifierConfig) {
    this.config = config;
  }

  verify(
    rawBody: string,
    signature: string | undefined,
    timestamp: string | undefined
  ): VerificationResult {
    const { enabled, publicKey, nodeEnv } = this.config;

    if (!enabled) {
      if (nodeEnv === "production") {
        logger.warn(
          "SendGrid webhook verification is disabled in production — requests are accepted without signature check"
        );
      }
      return { verified: false, skipped: true, reason: "verification_disabled" };
    }

    if (!publicKey) {
      logger.error("SendGrid webhook verification is enabled but no public key is configured");
      return { verified: false, skipped: false, reason: "missing_public_key" };
    }

    if (!signature || !timestamp) {
      logger.warn("SendGrid webhook request missing signature or timestamp headers");
      return { verified: false, skipped: false, reason: "missing_headers" };
    }

    try {
      const payload = timestamp + rawBody;
      const ecPublicKey = crypto.createPublicKey({
        key: Buffer.from(publicKey, "base64"),
        format: "der",
        type: "spki",
      });

      const verifier = crypto.createVerify("SHA256");
      verifier.update(payload, "utf8");
      const valid = verifier.verify(ecPublicKey, signature, "base64");

      if (!valid) {
        logger.warn("SendGrid webhook signature verification failed");
        return { verified: false, skipped: false, reason: "invalid_signature" };
      }

      return { verified: true, skipped: false };
    } catch (err) {
      logger.error("SendGrid webhook verification threw an error", { error: String(err) });
      return { verified: false, skipped: false, reason: `verification_error: ${String(err)}` };
    }
  }
}
