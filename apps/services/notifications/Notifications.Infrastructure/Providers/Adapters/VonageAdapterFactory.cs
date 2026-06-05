using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Domain;

namespace Notifications.Infrastructure.Providers.Adapters;

/// <summary>
/// LS-NOTIF-SMS-014: Factory for VonageAdapter instances from TenantProviderConfig.
///
/// Expected CredentialsJson shape:
///   { "apiKey": "...", "apiSecret": "..." }
///
/// Expected SettingsJson shape:
///   { "fromNumber": "+15551234567" }
///
/// Credentials are never logged or returned by this factory.
/// Vonage does not support a platform-default config (env-var-based) in this version.
/// </summary>
public class VonageAdapterFactory : ISmsProviderAdapterFactory
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<VonageAdapter> _adapterLogger;

    public VonageAdapterFactory(IHttpClientFactory httpFactory, ILogger<VonageAdapter> adapterLogger)
    {
        _httpFactory   = httpFactory;
        _adapterLogger = adapterLogger;
    }

    public bool Supports(string providerType)
        => string.Equals(providerType, "vonage", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public ISmsProviderAdapter CreateFromConfig(TenantProviderConfig config)
    {
        string apiKey, apiSecret, fromNumber;

        try
        {
            using var credDoc  = JsonDocument.Parse(config.CredentialsJson ?? "{}");
            var credRoot = credDoc.RootElement;
            apiKey    = credRoot.TryGetProperty("apiKey",    out var k) ? k.GetString() ?? "" : "";
            apiSecret = credRoot.TryGetProperty("apiSecret", out var s) ? s.GetString() ?? "" : "";
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"TenantProviderConfig {config.Id}: CredentialsJson is not valid JSON — {ex.Message}", ex);
        }

        try
        {
            using var settingsDoc = JsonDocument.Parse(config.SettingsJson ?? "{}");
            var settingsRoot = settingsDoc.RootElement;
            fromNumber = settingsRoot.TryGetProperty("fromNumber", out var f) ? f.GetString() ?? "" : "";
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"TenantProviderConfig {config.Id}: SettingsJson is not valid JSON — {ex.Message}", ex);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                $"TenantProviderConfig {config.Id}: 'apiKey' is missing in CredentialsJson");

        if (string.IsNullOrWhiteSpace(apiSecret))
            throw new InvalidOperationException(
                $"TenantProviderConfig {config.Id}: 'apiSecret' is missing in CredentialsJson");

        if (string.IsNullOrWhiteSpace(fromNumber))
            throw new InvalidOperationException(
                $"TenantProviderConfig {config.Id}: 'fromNumber' is missing in SettingsJson");

        return new VonageAdapter(
            apiKey,
            apiSecret,
            fromNumber,
            _httpFactory.CreateClient("Vonage"),
            _adapterLogger);
    }

    /// <summary>
    /// Vonage requires tenant-owned config (api_key/api_secret are tenant-specific).
    /// No platform-default supported in this version.
    /// </summary>
    public ISmsProviderAdapter? CreatePlatformDefault() => null;
}
