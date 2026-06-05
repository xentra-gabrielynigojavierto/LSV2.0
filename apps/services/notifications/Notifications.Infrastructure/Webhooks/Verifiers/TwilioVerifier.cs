using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Notifications.Infrastructure.Webhooks.Verifiers;

public class TwilioVerifier
{
    private readonly bool _enabled;
    private readonly string _authToken;
    private readonly string _environment;
    private readonly ILogger<TwilioVerifier> _logger;

    public TwilioVerifier(bool enabled, string authToken, string environment, ILogger<TwilioVerifier> logger)
    {
        _enabled = enabled;
        _authToken = authToken;
        _environment = environment;
        _logger = logger;
    }

    public (bool Verified, bool Skipped, string? Reason) Verify(string requestUrl, Dictionary<string, string> formParams, string? signature)
    {
        if (!_enabled)
        {
            _logger.LogWarning(
                "Twilio webhook verification is disabled; request rejected. " +
                "Set TWILIO_WEBHOOK_VERIFICATION_ENABLED=true and configure " +
                "TWILIO_AUTH_TOKEN to accept webhook callbacks.");
            return (false, false, "verification_disabled");
        }

        if (string.IsNullOrEmpty(_authToken))
        {
            _logger.LogError("Twilio webhook verification is enabled but no auth token is configured");
            return (false, false, "missing_auth_token");
        }

        if (string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Twilio webhook request missing X-Twilio-Signature header");
            return (false, false, "missing_signature");
        }

        try
        {
            var sortedParams = string.Join("", formParams.OrderBy(kv => kv.Key).Select(kv => kv.Key + kv.Value));
            var dataToSign = requestUrl + sortedParams;

            using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_authToken));
            var expectedSig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign)));

            var valid = CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(signature),
                Convert.FromBase64String(expectedSig));

            if (!valid)
            {
                _logger.LogWarning("Twilio webhook signature verification failed");
                return (false, false, "invalid_signature");
            }

            return (true, false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio webhook verification threw an error");
            return (false, false, $"verification_error: {ex.Message}");
        }
    }
}
