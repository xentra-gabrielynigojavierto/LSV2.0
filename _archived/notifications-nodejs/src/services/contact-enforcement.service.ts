import { ContactSuppressionRepository } from "../repositories/contact-suppression.repository";
import { TenantContactPolicyRepository, DEFAULT_CONTACT_POLICY } from "../repositories/tenant-contact-policy.repository";
import { RecipientContactHealthRepository } from "../repositories/recipient-contact-health.repository";
import { normalizeContactValue } from "../shared/contact-normalizer";
import { auditClient } from "../integrations/audit/audit.client";
import { logger } from "../shared/logger";
import {
  NotificationChannel,
  ContactEnforcementResult,
  SuppressionType,
  NON_OVERRIDEABLE_SUPPRESSION_TYPES,
} from "../types";

const suppressionRepo = new ContactSuppressionRepository();
const policyRepo = new TenantContactPolicyRepository();
const healthRepo = new RecipientContactHealthRepository();

// Health statuses that map to blocking behavior by policy field
const HEALTH_STATUS_POLICY_MAP: Record<string, keyof typeof DEFAULT_CONTACT_POLICY> = {
  bounced: "blockBouncedContacts",
  complained: "blockComplainedContacts",
  unsubscribed: "blockUnsubscribedContacts",
  suppressed: "blockSuppressedContacts",
  invalid: "blockInvalidContacts",
  carrier_rejected: "blockCarrierRejectedContacts",
  opted_out: "blockUnsubscribedContacts",
};

// Suppression types that map to blocking behavior by policy field
const SUPPRESSION_TYPE_POLICY_MAP: Record<SuppressionType, keyof typeof DEFAULT_CONTACT_POLICY> = {
  manual: "blockSuppressedContacts",
  bounce: "blockBouncedContacts",
  unsubscribe: "blockUnsubscribedContacts",
  complaint: "blockComplainedContacts",
  invalid_contact: "blockInvalidContacts",
  carrier_rejection: "blockCarrierRejectedContacts",
  system_protection: "blockSuppressedContacts",
};

export interface ContactEnforcementInput {
  tenantId: string;
  channel: NotificationChannel;
  contactValue: string;
  overrideSuppression?: boolean;
  overrideReason?: string;
}

/**
 * Evaluate whether a send to a contact is allowed under the current tenant policy
 * and active suppressions.
 *
 * Default behavior when no policy is configured:
 * - Block unsubscribed, complained, and explicitly suppressed contacts (compliance)
 * - Allow bounced, invalid, carrier_rejected (retriable by default)
 * - Override is NOT allowed by default
 */
export async function evaluateContactEnforcement(
  input: ContactEnforcementInput
): Promise<ContactEnforcementResult> {
  const { tenantId, channel, overrideSuppression, overrideReason } = input;
  const normalizedContact = normalizeContactValue(channel, input.contactValue);

  const defaultResult: ContactEnforcementResult = {
    allowed: true,
    reasonCode: null,
    reasonMessage: "Contact is allowed to receive notifications",
    matchedHealthStatus: null,
    matchedSuppressionId: null,
    overrideAllowed: false,
    overrideUsed: false,
  };

  try {
    // Load policy (channel-specific → global → built-in defaults)
    const dbPolicy = await policyRepo.findEffectivePolicy(tenantId, channel);
    const policy = dbPolicy
      ? {
          blockSuppressedContacts: dbPolicy.blockSuppressedContacts,
          blockUnsubscribedContacts: dbPolicy.blockUnsubscribedContacts,
          blockComplainedContacts: dbPolicy.blockComplainedContacts,
          blockBouncedContacts: dbPolicy.blockBouncedContacts,
          blockInvalidContacts: dbPolicy.blockInvalidContacts,
          blockCarrierRejectedContacts: dbPolicy.blockCarrierRejectedContacts,
          allowManualOverride: dbPolicy.allowManualOverride,
        }
      : DEFAULT_CONTACT_POLICY;

    // ── 1. Check active suppressions ──────────────────────────────────────────
    const suppressions = await suppressionRepo.findActive(tenantId, channel, normalizedContact);

    for (const suppression of suppressions) {
      const policyField = SUPPRESSION_TYPE_POLICY_MAP[suppression.suppressionType];
      const isBlocked = policyField ? policy[policyField] : policy.blockSuppressedContacts;

      if (!isBlocked) continue;

      const isOverrideable = !NON_OVERRIDEABLE_SUPPRESSION_TYPES.includes(suppression.suppressionType);
      const overrideAttempted = overrideSuppression === true && !!overrideReason;
      const overrideGranted = isOverrideable && policy.allowManualOverride && overrideAttempted;

      if (overrideGranted) {
        // Override used — allowed but must be audited
        return {
          allowed: true,
          reasonCode: `override_${suppression.suppressionType}`,
          reasonMessage: `Override applied for suppression type: ${suppression.suppressionType}`,
          matchedHealthStatus: null,
          matchedSuppressionId: suppression.id,
          overrideAllowed: true,
          overrideUsed: true,
        };
      }

      return {
        allowed: false,
        reasonCode: `suppressed_${suppression.suppressionType}`,
        reasonMessage: `Contact is suppressed: ${suppression.suppressionType} (${suppression.reason})`,
        matchedHealthStatus: null,
        matchedSuppressionId: suppression.id,
        overrideAllowed: isOverrideable && policy.allowManualOverride,
        overrideUsed: false,
      };
    }

    // ── 2. Check contact health record ────────────────────────────────────────
    const health = await healthRepo.findByContact(tenantId, channel, normalizedContact);
    if (health && health.healthStatus !== "valid" && health.healthStatus !== "unreachable") {
      const policyField = HEALTH_STATUS_POLICY_MAP[health.healthStatus];
      const isBlocked = policyField ? policy[policyField] : false;

      if (isBlocked) {
        const statusType = health.healthStatus as SuppressionType | undefined;
        const isOverrideable = statusType
          ? !NON_OVERRIDEABLE_SUPPRESSION_TYPES.includes(statusType as SuppressionType)
          : true;
        const overrideAttempted = overrideSuppression === true && !!overrideReason;
        const overrideGranted = isOverrideable && policy.allowManualOverride && overrideAttempted;

        if (overrideGranted) {
          return {
            allowed: true,
            reasonCode: `override_health_${health.healthStatus}`,
            reasonMessage: `Override applied for contact health: ${health.healthStatus}`,
            matchedHealthStatus: health.healthStatus,
            matchedSuppressionId: null,
            overrideAllowed: true,
            overrideUsed: true,
          };
        }

        return {
          allowed: false,
          reasonCode: `health_${health.healthStatus}`,
          reasonMessage: `Contact health status is '${health.healthStatus}' and is blocked by policy`,
          matchedHealthStatus: health.healthStatus,
          matchedSuppressionId: null,
          overrideAllowed: isOverrideable && policy.allowManualOverride,
          overrideUsed: false,
        };
      }
    }

    return defaultResult;
  } catch (err) {
    // Missing policy or DB error must NOT crash sends — fail open
    logger.error("ContactEnforcement: check failed, allowing send", { error: String(err), tenantId, channel });
    return { ...defaultResult, reasonMessage: "Contact enforcement check failed — defaulting to allow" };
  }
}
