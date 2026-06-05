using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Domain;

namespace Notifications.Infrastructure.Providers.Adapters;

/// <summary>
/// Creates <see cref="TwilioAdapter"/> instances from <see cref="TenantProviderConfig"/>
/// by parsing CredentialsJson and SettingsJson.
///
/// Expected CredentialsJson shape:
///   { "accountSid": "ACxxxxxxxx...", "authToken": "..." }
///
/// Expected SettingsJson shape:
///   { "fromNumber": "+15551234567" }
///
/// Credentials are never logged or returned by this factory.
/// </summary>
public class TwilioAdapterFactory : ITwilioAdapterFactory
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<TwilioAdapter> _adapterLogger;

    public TwilioAdapterFactory(IHttpClientFactory httpFactory, ILogger<TwilioAdapter> adapterLogger)
    {
        _httpFactory    = httpFactory;
        _adapterLogger  = adapterLogger;
    }

    /// <inheritdoc />
    public ISmsProviderAdapter CreateFromConfig(TenantProviderConfig config)
    {
        string accountSid, authToken, fromNumber;

        try
        {
            using var credDoc  = JsonDocument.Parse(config.CredentialsJson ?? "{}");
            var credRoot = credDoc.RootElement;

            accountSid = credRoot.TryGetProperty("accountSid", out var s) ? s.GetString() ?? "" : "";
            authToken  = credRoot.TryGetProperty("authToken",  out var t) ? t.GetString() ?? "" : "";
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

        if (string.IsNullOrWhiteSpace(accountSid))
            throw new InvalidOperationException(
                $"TenantProviderConfig {config.Id}: 'accountSid' is missing in CredentialsJson");

        if (string.IsNullOrWhiteSpace(authToken))
            throw new InvalidOperationException(
                $"TenantProviderConfig {config.Id}: 'authToken' is missing in CredentialsJson");

        if (string.IsNullOrWhiteSpace(fromNumber))
            throw new InvalidOperationException(
                $"TenantProviderConfig {config.Id}: 'fromNumber' is missing in SettingsJson");

        return new TwilioAdapter(
            accountSid,
            authToken,
            fromNumber,
            _httpFactory.CreateClient("Twilio"),
            _adapterLogger);
    }
}
