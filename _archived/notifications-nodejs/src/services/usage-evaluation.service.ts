import { TenantRateLimitPolicyRepository } from "../repositories/tenant-rate-limit-policy.repository";
import { UsageMeterEventRepository } from "../repositories/usage-meter-event.repository";
import { auditClient } from "../integrations/audit/audit.client";
import { logger } from "../shared/logger";
import { NotificationChannel, EnforcementDecision } from "../types";

const rateLimitRepo = new TenantRateLimitPolicyRepository();
const usageRepo = new UsageMeterEventRepository();

function startOfDay(date: Date): Date {
  const d = new Date(date);
  d.setUTCHours(0, 0, 0, 0);
  return d;
}

function startOfMonth(date: Date): Date {
  const d = new Date(date);
  d.setUTCDate(1);
  d.setUTCHours(0, 0, 0, 0);
  return d;
}

function oneMinuteAgo(): Date {
  return new Date(Date.now() - 60_000);
}

/**
 * Check whether a new notification request is allowed for a tenant/channel.
 * Returns allowed=true by default when no policy exists (fail-open on missing policy).
 */
export async function checkRequestAllowed(
  tenantId: string,
  channel: NotificationChannel
): Promise<EnforcementDecision> {
  try {
    const policies = await rateLimitRepo.findActivePolicies(tenantId, channel);
    if (policies.length === 0) return { allowed: true };

    const now = new Date();

    for (const policy of policies) {
      // ── Per-minute request cap ──────────────────────────────────────────────
      if (policy.maxRequestsPerMinute !== null) {
        const recentRequests = await usageRepo.countSince(
          tenantId,
          "api_notification_request",
          oneMinuteAgo(),
          policy.channel ?? undefined
        );
        if (recentRequests >= policy.maxRequestsPerMinute) {
          logger.warn("Rate limit exceeded: maxRequestsPerMinute", {
            tenantId,
            channel,
            recentRequests,
            limit: policy.maxRequestsPerMinute,
          });
          await auditClient.publishEvent({
            eventType: "rate_limit.exceeded",
            tenantId,
            channel,
            metadata: { type: "maxRequestsPerMinute", recentRequests, limit: policy.maxRequestsPerMinute },
          });
          return {
            allowed: false,
            reason: `Rate limit exceeded: ${recentRequests}/${policy.maxRequestsPerMinute} requests in the last minute`,
            code: "RATE_LIMIT_EXCEEDED",
          };
        }
      }

      // ── Daily quota ──────────────────────────────────────────────────────────
      if (policy.maxDailyUsage !== null) {
        const todayStart = startOfDay(now);
        const dailyCount = await usageRepo.countSince(
          tenantId,
          "api_notification_request",
          todayStart,
          policy.channel ?? undefined
        );
        if (dailyCount >= policy.maxDailyUsage) {
          logger.warn("Quota exceeded: maxDailyUsage", { tenantId, channel, dailyCount, limit: policy.maxDailyUsage });
          await auditClient.publishEvent({
            eventType: "quota.exceeded",
            tenantId,
            channel,
            metadata: { type: "maxDailyUsage", dailyCount, limit: policy.maxDailyUsage },
          });
          return {
            allowed: false,
            reason: `Daily quota exceeded: ${dailyCount}/${policy.maxDailyUsage} requests today`,
            code: "DAILY_QUOTA_EXCEEDED",
          };
        }
      }

      // ── Monthly quota ────────────────────────────────────────────────────────
      if (policy.maxMonthlyUsage !== null) {
        const monthStart = startOfMonth(now);
        const monthlyCount = await usageRepo.countSince(
          tenantId,
          "api_notification_request",
          monthStart,
          policy.channel ?? undefined
        );
        if (monthlyCount >= policy.maxMonthlyUsage) {
          logger.warn("Quota exceeded: maxMonthlyUsage", { tenantId, channel, monthlyCount, limit: policy.maxMonthlyUsage });
          await auditClient.publishEvent({
            eventType: "quota.exceeded",
            tenantId,
            channel,
            metadata: { type: "maxMonthlyUsage", monthlyCount, limit: policy.maxMonthlyUsage },
          });
          return {
            allowed: false,
            reason: `Monthly quota exceeded: ${monthlyCount}/${policy.maxMonthlyUsage} requests this month`,
            code: "MONTHLY_QUOTA_EXCEEDED",
          };
        }
      }
    }

    return { allowed: true };
  } catch (err) {
    // Missing or broken policy must NOT crash sends — fail open
    logger.error("UsageEvaluation: enforcement check failed, allowing request", { error: String(err), tenantId, channel });
    return { allowed: true };
  }
}

/**
 * Check per-minute attempt cap (called before creating an outbound attempt).
 * Used when maxAttemptsPerMinute is relevant.
 */
export async function checkAttemptAllowed(
  tenantId: string,
  channel: NotificationChannel
): Promise<EnforcementDecision> {
  try {
    const policies = await rateLimitRepo.findActivePolicies(tenantId, channel);
    if (policies.length === 0) return { allowed: true };

    for (const policy of policies) {
      if (policy.maxAttemptsPerMinute !== null) {
        const attemptUnits = channel === "email"
          ? (["email_attempt"] as const)
          : (["sms_attempt"] as const);

        const recentAttempts = await usageRepo.countSinceMultiple(
          tenantId,
          [...attemptUnits],
          oneMinuteAgo(),
          policy.channel ?? undefined
        );
        if (recentAttempts >= policy.maxAttemptsPerMinute) {
          logger.warn("Rate limit exceeded: maxAttemptsPerMinute", {
            tenantId,
            channel,
            recentAttempts,
            limit: policy.maxAttemptsPerMinute,
          });
          return {
            allowed: false,
            reason: `Attempt rate limit exceeded: ${recentAttempts}/${policy.maxAttemptsPerMinute} attempts in last minute`,
            code: "RATE_LIMIT_EXCEEDED",
          };
        }
      }
    }

    return { allowed: true };
  } catch (err) {
    logger.error("UsageEvaluation: attempt check failed, allowing", { error: String(err), tenantId, channel });
    return { allowed: true };
  }
}
