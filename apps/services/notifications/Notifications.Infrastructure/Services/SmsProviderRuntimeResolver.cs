using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// Resolves the correct SMS provider adapter at runtime.
///
/// Send-time: routes with TenantProviderConfigId → tenant adapter via ISmsProviderAdapterRegistry.
///            routes without TenantProviderConfigId → platform adapter.
///
/// Reconciliation-time: uses attempt.ProviderConfigId to reload the exact config
///                      used for the original send. Falls back to platform adapter
///                      for platform-owned attempts (ProviderConfigId = null).
///
/// LS-NOTIF-SMS-014: BuildAdapter() now uses ISmsProviderAdapterRegistry instead of a
/// hard-coded "twilio"-only switch. New providers (Vonage, Telnyx, etc.) are
/// automatically supported when their ISmsProviderAdapterFactory is registered.
///
/// Credentials are never exposed outside adapter factories.
/// Provider config failures return structured SmsProviderRuntimeContext — never throw.
/// </summary>
public class SmsProviderRuntimeResolver : ISmsProviderRuntimeResolver
{
    private readonly ITenantProviderConfigRepository _configRepo;
    private readonly ITwilioAdapterFactory _twilioFactory;
    private readonly ISmsProviderAdapterRegistry _adapterRegistry;
    private readonly ISmsProviderAdapter _platformAdapter;
    private readonly ILogger<SmsProviderRuntimeResolver> _logger;

    public SmsProviderRuntimeResolver(
        ITenantProviderConfigRepository configRepo,
        ITwilioAdapterFactory twilioFactory,
        ISmsProviderAdapterRegistry adapterRegistry,
        ISmsProviderAdapter platformAdapter,
        ILogger<SmsProviderRuntimeResolver> logger)
    {
        _configRepo      = configRepo;
        _twilioFactory   = twilioFactory;
        _adapterRegistry = adapterRegistry;
        _platformAdapter = platformAdapter;
        _logger          = logger;
    }

    // ── Send-time resolution ──────────────────────────────────────────────────

    public async Task<SmsProviderRuntimeContext> ResolveForSendAsync(
        Guid tenantId,
        string providerType,
        Guid? providerConfigId,
        CancellationToken cancellationToken = default)
    {
        // No tenant config ID on the route → platform-owned route.
        if (!providerConfigId.HasValue)
        {
            _logger.LogDebug(
                "SMS runtime: no TenantProviderConfigId on route — using platform adapter (tenant={TenantId}, provider={Provider})",
                tenantId, providerType);

            return new SmsProviderRuntimeContext
            {
                Success              = true,
                ProviderType         = providerType,
                TenantId             = tenantId,
                OwnershipMode        = "platform",
                UsedPlatformFallback = false,
                Adapter              = _platformAdapter,
            };
        }

        // Tenant-owned config → load, validate, build adapter.
        var config = await _configRepo.FindByIdAndTenantAsync(providerConfigId.Value, tenantId);
        if (config == null)
        {
            _logger.LogWarning(
                "SMS runtime: TenantProviderConfig {ConfigId} not found for tenant {TenantId}",
                providerConfigId, tenantId);
            return SmsProviderRuntimeContext.Failure(
                "provider_config_not_found",
                $"SMS provider config {providerConfigId} not found for tenant {tenantId}",
                false);
        }

        if (config.Status != "active")
        {
            _logger.LogWarning(
                "SMS runtime: TenantProviderConfig {ConfigId} is not active (status={Status})",
                providerConfigId, config.Status);
            return SmsProviderRuntimeContext.Failure(
                "provider_config_inactive",
                $"SMS provider config {providerConfigId} is {config.Status}",
                false);
        }

        ISmsProviderAdapter adapter;
        try
        {
            adapter = BuildAdapter(providerType, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SMS runtime: failed to build adapter from TenantProviderConfig {ConfigId}",
                providerConfigId);
            return SmsProviderRuntimeContext.Failure("provider_config_invalid", ex.Message, false);
        }

        if (!await adapter.ValidateConfigAsync())
        {
            _logger.LogWarning(
                "SMS runtime: TenantProviderConfig {ConfigId} failed adapter validation (missing required credentials or settings)",
                providerConfigId);
            return SmsProviderRuntimeContext.Failure(
                "provider_config_invalid",
                "Required SMS provider credentials or settings are missing or empty",
                false);
        }

        _logger.LogDebug(
            "SMS runtime: resolved tenant adapter for config {ConfigId} (tenant={TenantId}, provider={Provider})",
            providerConfigId, tenantId, providerType);

        return new SmsProviderRuntimeContext
        {
            Success              = true,
            ProviderType         = providerType,
            TenantId             = tenantId,
            ProviderConfigId     = providerConfigId,
            OwnershipMode        = "tenant",
            UsedPlatformFallback = false,
            Adapter              = adapter,
        };
    }

    // ── Reconciliation-time resolution ────────────────────────────────────────

    public async Task<SmsProviderRuntimeContext> ResolveForReconciliationAsync(
        Guid? tenantId,
        string providerType,
        Guid? providerConfigId,
        CancellationToken cancellationToken = default)
    {
        // No providerConfigId on the attempt → was a platform-owned send.
        if (!providerConfigId.HasValue)
        {
            if (!tenantId.HasValue)
            {
                _logger.LogWarning(
                    "SMS reconciliation runtime: attempt has no ProviderConfigId and no TenantId — cannot resolve adapter");
                return SmsProviderRuntimeContext.Failure(
                    "missing_provider_config_context",
                    "Attempt has no providerConfigId or tenantId — cannot determine SMS provider context",
                    false);
            }

            // Platform-owned attempt → use platform adapter.
            _logger.LogDebug(
                "SMS reconciliation runtime: no ProviderConfigId on attempt — using platform adapter (tenant={TenantId}, provider={Provider})",
                tenantId, providerType);

            return new SmsProviderRuntimeContext
            {
                Success              = true,
                ProviderType         = providerType,
                TenantId             = tenantId,
                OwnershipMode        = "platform",
                UsedPlatformFallback = true,
                Adapter              = _platformAdapter,
            };
        }

        // Load the exact config used for the original send.
        Domain.TenantProviderConfig? config;
        if (tenantId.HasValue)
            config = await _configRepo.FindByIdAndTenantAsync(providerConfigId.Value, tenantId.Value);
        else
            config = await _configRepo.GetByIdAsync(providerConfigId.Value);

        if (config == null)
        {
            _logger.LogWarning(
                "SMS reconciliation runtime: TenantProviderConfig {ConfigId} not found",
                providerConfigId);
            return SmsProviderRuntimeContext.Failure(
                "provider_config_not_found",
                $"SMS provider config {providerConfigId} not found",
                false);
        }

        if (config.Status != "active")
        {
            _logger.LogWarning(
                "SMS reconciliation runtime: TenantProviderConfig {ConfigId} is not active (status={Status}) — reconciliation cannot proceed",
                providerConfigId, config.Status);
            return SmsProviderRuntimeContext.Failure(
                "provider_config_inactive",
                $"SMS provider config {providerConfigId} is {config.Status}",
                false);
        }

        ISmsProviderAdapter adapter;
        try
        {
            adapter = BuildAdapter(providerType, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SMS reconciliation runtime: failed to build adapter from TenantProviderConfig {ConfigId}",
                providerConfigId);
            return SmsProviderRuntimeContext.Failure("provider_config_invalid", ex.Message, false);
        }

        _logger.LogDebug(
            "SMS reconciliation runtime: resolved tenant adapter for config {ConfigId} (tenant={TenantId}, provider={Provider})",
            providerConfigId, config.TenantId, providerType);

        return new SmsProviderRuntimeContext
        {
            Success              = true,
            ProviderType         = providerType,
            TenantId             = config.TenantId,
            ProviderConfigId     = config.Id,
            OwnershipMode        = "tenant",
            UsedPlatformFallback = false,
            Adapter              = adapter,
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// LS-NOTIF-SMS-014: Delegate to ISmsProviderAdapterRegistry instead of a
    /// hard-coded switch. Supports Twilio, Vonage, and any future providers
    /// registered via ISmsProviderAdapterFactory.
    /// </summary>
    private ISmsProviderAdapter BuildAdapter(string providerType, Domain.TenantProviderConfig config)
        => _adapterRegistry.BuildAdapter(providerType, config);
}
