using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

public class UsageEvaluationService : IUsageEvaluationService
{
    private readonly ITenantRateLimitPolicyRepository _rateLimitRepo;
    private readonly IUsageMeterEventRepository _usageRepo;
    private readonly ILogger<UsageEvaluationService> _logger;

    public UsageEvaluationService(ITenantRateLimitPolicyRepository rateLimitRepo, IUsageMeterEventRepository usageRepo, ILogger<UsageEvaluationService> logger)
    {
        _rateLimitRepo = rateLimitRepo;
        _usageRepo = usageRepo;
        _logger = logger;
    }

    public async Task<EnforcementDecision> CheckRequestAllowedAsync(Guid tenantId, string channel)
    {
        try
        {
            var policies = await _rateLimitRepo.FindActivePoliciesAsync(tenantId, channel);
            if (policies.Count == 0) return new EnforcementDecision { Allowed = true };

            var now = DateTime.UtcNow;
            foreach (var policy in policies)
            {
                if (policy.MaxRequestsPerMinute.HasValue)
                {
                    var recent = await _usageRepo.CountSinceAsync(tenantId, "api_notification_request", now.AddMinutes(-1), policy.Channel);
                    if (recent >= policy.MaxRequestsPerMinute.Value)
                    {
                        _logger.LogWarning("Rate limit exceeded: maxRequestsPerMinute {TenantId} {Channel}", tenantId, channel);
                        return new EnforcementDecision { Allowed = false, Reason = $"Rate limit exceeded: {recent}/{policy.MaxRequestsPerMinute} requests in the last minute", Code = "RATE_LIMIT_EXCEEDED" };
                    }
                }
                if (policy.MaxDailyUsage.HasValue)
                {
                    var todayStart = now.Date;
                    var daily = await _usageRepo.CountSinceAsync(tenantId, "api_notification_request", todayStart, policy.Channel);
                    if (daily >= policy.MaxDailyUsage.Value)
                        return new EnforcementDecision { Allowed = false, Reason = $"Daily quota exceeded: {daily}/{policy.MaxDailyUsage} requests today", Code = "DAILY_QUOTA_EXCEEDED" };
                }
                if (policy.MaxMonthlyUsage.HasValue)
                {
                    var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    var monthly = await _usageRepo.CountSinceAsync(tenantId, "api_notification_request", monthStart, policy.Channel);
                    if (monthly >= policy.MaxMonthlyUsage.Value)
                        return new EnforcementDecision { Allowed = false, Reason = $"Monthly quota exceeded: {monthly}/{policy.MaxMonthlyUsage} requests this month", Code = "MONTHLY_QUOTA_EXCEEDED" };
                }
            }
            return new EnforcementDecision { Allowed = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UsageEvaluation: enforcement check failed, allowing request");
            return new EnforcementDecision { Allowed = true };
        }
    }

    public async Task<EnforcementDecision> CheckAttemptAllowedAsync(Guid tenantId, string channel)
    {
        try
        {
            var policies = await _rateLimitRepo.FindActivePoliciesAsync(tenantId, channel);
            if (policies.Count == 0) return new EnforcementDecision { Allowed = true };

            foreach (var policy in policies)
            {
                if (policy.MaxAttemptsPerMinute.HasValue)
                {
                    var units = channel == "email" ? new[] { "email_attempt" } : new[] { "sms_attempt" };
                    var recent = await _usageRepo.CountSinceMultipleAsync(tenantId, units, DateTime.UtcNow.AddMinutes(-1), policy.Channel);
                    if (recent >= policy.MaxAttemptsPerMinute.Value)
                        return new EnforcementDecision { Allowed = false, Reason = $"Attempt rate limit exceeded: {recent}/{policy.MaxAttemptsPerMinute} attempts in last minute", Code = "RATE_LIMIT_EXCEEDED" };
                }
            }
            return new EnforcementDecision { Allowed = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UsageEvaluation: attempt check failed, allowing");
            return new EnforcementDecision { Allowed = true };
        }
    }
}
