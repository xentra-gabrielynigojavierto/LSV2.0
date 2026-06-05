using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Domain;

namespace Notifications.Infrastructure.Services;

public class UsageMeteringService : IUsageMeteringService
{
    private readonly IUsageMeterEventRepository _repo;
    private readonly ITenantBillingPlanRepository _billingRepo;
    private readonly ITenantBillingRateRepository _rateRepo;
    private readonly ILogger<UsageMeteringService> _logger;

    public UsageMeteringService(IUsageMeterEventRepository repo, ITenantBillingPlanRepository billingRepo, ITenantBillingRateRepository rateRepo, ILogger<UsageMeteringService> logger)
    {
        _repo = repo;
        _billingRepo = billingRepo;
        _rateRepo = rateRepo;
        _logger = logger;
    }

    public async Task MeterAsync(MeterEventInput input)
    {
        try
        {
            var (isBillable, currency) = await EvaluateBillingAsync(input.TenantId, input.UsageUnit, input.Channel, input.ProviderOwnershipMode);
            await _repo.CreateSilentAsync(new UsageMeterEvent
            {
                TenantId = input.TenantId,
                NotificationId = input.NotificationId,
                NotificationAttemptId = input.NotificationAttemptId,
                Channel = input.Channel,
                Provider = input.Provider,
                ProviderOwnershipMode = input.ProviderOwnershipMode,
                ProviderConfigId = input.ProviderConfigId,
                UsageUnit = input.UsageUnit,
                Quantity = input.Quantity,
                IsBillable = isBillable,
                ProviderUnitCost = input.ProviderUnitCost,
                ProviderTotalCost = input.ProviderTotalCost,
                Currency = input.Currency ?? currency,
                MetadataJson = input.Metadata != null ? System.Text.Json.JsonSerializer.Serialize(input.Metadata) : null,
                OccurredAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Metering] Failed to record meter event: {UsageUnit}", input.UsageUnit);
        }
    }

    public async Task MeterBatchAsync(IEnumerable<MeterEventInput> inputs)
    {
        foreach (var input in inputs)
            await MeterAsync(input);
    }

    private async Task<(bool IsBillable, string? Currency)> EvaluateBillingAsync(Guid tenantId, string usageUnit, string? channel, string? providerOwnershipMode)
    {
        try
        {
            var plan = await _billingRepo.FindActivePlanAsync(tenantId);
            if (plan == null) return (false, null);

            var rate = await _rateRepo.FindRateAsync(plan.Id, usageUnit, channel, providerOwnershipMode);
            if (rate != null) return (rate.IsBillable, rate.Currency);

            var fallbackRate = await _rateRepo.FindRateAsync(plan.Id, usageUnit);
            if (fallbackRate != null) return (fallbackRate.IsBillable, fallbackRate.Currency);

            return (false, plan.Currency);
        }
        catch
        {
            return (false, null);
        }
    }
}
