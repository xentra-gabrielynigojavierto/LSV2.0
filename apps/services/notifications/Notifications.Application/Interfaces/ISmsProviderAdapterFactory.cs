using Notifications.Domain;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-014: Generic factory interface for SMS provider adapters.
/// Each provider adapter implementation has a corresponding factory that implements this interface.
/// TwilioAdapterFactory, VonageAdapterFactory, etc. all implement this contract.
///
/// Provider-specific logic (credential parsing, field validation) stays in the concrete factory.
/// Credentials are never returned or logged.
/// </summary>
public interface ISmsProviderAdapterFactory
{
    /// <summary>Returns true if this factory can create an adapter for the given provider type.</summary>
    bool Supports(string providerType);

    /// <summary>
    /// Create an <see cref="ISmsProviderAdapter"/> from a tenant-owned config.
    /// Throws <see cref="InvalidOperationException"/> if required credentials or settings are missing.
    /// </summary>
    ISmsProviderAdapter CreateFromConfig(TenantProviderConfig config);

    /// <summary>
    /// Create a platform-default adapter using env/config credentials (not TenantProviderConfig).
    /// Returns null if the factory cannot create a platform-default for this provider type
    /// (e.g., provider requires tenant-owned config only).
    /// </summary>
    ISmsProviderAdapter? CreatePlatformDefault();
}

/// <summary>
/// Registry of SMS provider adapter factories.
/// Used by SmsProviderRuntimeResolver to build adapters from TenantProviderConfig
/// without a hard-coded provider switch.
/// </summary>
public interface ISmsProviderAdapterRegistry
{
    /// <summary>
    /// Build an adapter for the given provider type from a tenant-owned config.
    /// Throws <see cref="NotSupportedException"/> if no factory supports the provider.
    /// Throws <see cref="InvalidOperationException"/> if credentials/settings are invalid.
    /// </summary>
    ISmsProviderAdapter BuildAdapter(string providerType, TenantProviderConfig config);

    /// <summary>Returns true if any registered factory supports the given provider type.</summary>
    bool IsSupported(string providerType);
}
