using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Domain;

namespace Notifications.Infrastructure.Providers.Adapters;

/// <summary>
/// LS-NOTIF-SMS-014: Wraps TwilioAdapterFactory to also implement ISmsProviderAdapterFactory.
///
/// TwilioAdapterFactory already implements ITwilioAdapterFactory (existing interface).
/// This wrapper adds the generic ISmsProviderAdapterFactory contract so TwilioAdapter
/// participates in the new SmsProviderAdapterRegistry without changing existing code.
///
/// Preserves backward compatibility: ITwilioAdapterFactory is still registered separately
/// for the existing SmsProviderRuntimeResolver injection.
/// </summary>
public class TwilioAdapterFactoryWrapper : ISmsProviderAdapterFactory
{
    private readonly ITwilioAdapterFactory _inner;
    private readonly IConfiguration        _config;
    private readonly ILogger<TwilioAdapter> _adapterLogger;
    private readonly IHttpClientFactory     _httpFactory;

    public TwilioAdapterFactoryWrapper(
        ITwilioAdapterFactory inner,
        IConfiguration config,
        IHttpClientFactory httpFactory,
        ILogger<TwilioAdapter> adapterLogger)
    {
        _inner         = inner;
        _config        = config;
        _httpFactory   = httpFactory;
        _adapterLogger = adapterLogger;
    }

    public bool Supports(string providerType)
        => string.Equals(providerType, "twilio", StringComparison.OrdinalIgnoreCase);

    public ISmsProviderAdapter CreateFromConfig(TenantProviderConfig config)
        => _inner.CreateFromConfig(config);

    /// <summary>
    /// Returns the platform Twilio adapter using env/config credentials.
    /// </summary>
    public ISmsProviderAdapter? CreatePlatformDefault()
    {
        var sid    = _config["TWILIO_ACCOUNT_SID"] ?? "";
        var token  = _config["TWILIO_AUTH_TOKEN"]  ?? "";
        var from   = _config["TWILIO_FROM_NUMBER"] ?? "";
        return new TwilioAdapter(sid, token, from, _httpFactory.CreateClient("Twilio"), _adapterLogger);
    }
}
