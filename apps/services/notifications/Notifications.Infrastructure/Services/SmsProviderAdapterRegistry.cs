using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Domain;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-014: Registry of SMS provider adapter factories.
/// Replaces the hard-coded "twilio"-only switch in SmsProviderRuntimeResolver.BuildAdapter().
///
/// All ISmsProviderAdapterFactory implementations are injected via IEnumerable.
/// Factories declare which provider types they support via Supports().
///
/// Provider-specific credential parsing stays in the concrete factory.
/// This registry has no knowledge of credentials.
/// </summary>
public class SmsProviderAdapterRegistry : ISmsProviderAdapterRegistry
{
    private readonly IReadOnlyList<ISmsProviderAdapterFactory> _factories;
    private readonly ILogger<SmsProviderAdapterRegistry> _logger;

    public SmsProviderAdapterRegistry(
        IEnumerable<ISmsProviderAdapterFactory> factories,
        ILogger<SmsProviderAdapterRegistry> logger)
    {
        _factories = factories.ToList();
        _logger    = logger;
    }

    public bool IsSupported(string providerType)
        => _factories.Any(f => f.Supports(providerType));

    public ISmsProviderAdapter BuildAdapter(string providerType, TenantProviderConfig config)
    {
        var factory = _factories.FirstOrDefault(f => f.Supports(providerType));

        if (factory == null)
        {
            _logger.LogWarning(
                "SmsProviderAdapterRegistry: no factory registered for provider type '{ProviderType}'",
                providerType);
            throw new NotSupportedException(
                $"No SMS provider adapter factory registered for provider type '{providerType}'. " +
                $"Registered types: {string.Join(", ", _factories.SelectMany(f => new[] { f.GetType().Name }))}");
        }

        return factory.CreateFromConfig(config);
    }
}
