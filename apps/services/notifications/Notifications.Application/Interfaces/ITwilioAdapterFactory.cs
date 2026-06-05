using Notifications.Domain;

namespace Notifications.Application.Interfaces;

/// <summary>
/// Factory for creating SMS provider adapters from tenant provider configuration.
/// Used by <see cref="ISmsProviderRuntimeResolver"/> to build per-tenant Twilio adapters
/// without duplicating TwilioAdapter send/status logic.
///
/// Credentials in <paramref name="config"/> are never returned or logged by this factory.
/// </summary>
public interface ITwilioAdapterFactory
{
    /// <summary>
    /// Create a <see cref="ISmsProviderAdapter"/> (and <see cref="ISmsProviderStatusLookup"/>)
    /// from a tenant-owned <see cref="TenantProviderConfig"/>.
    ///
    /// Reads <c>CredentialsJson</c> for <c>accountSid</c> and <c>authToken</c>,
    /// and <c>SettingsJson</c> for <c>fromNumber</c>.
    ///
    /// Throws <see cref="InvalidOperationException"/> if required fields are missing or JSON is malformed.
    /// The caller is responsible for catching and converting to a structured failure.
    /// </summary>
    ISmsProviderAdapter CreateFromConfig(TenantProviderConfig config);
}
