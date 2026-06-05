using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Notifications.Infrastructure.Webhooks.Verifiers;

public class SendGridVerifier
{
    private readonly bool _enabled;
    private readonly string _publicKey;
    private readonly string _environment;
    private readonly ILogger<SendGridVerifier> _logger;

    public SendGridVerifier(bool enabled, string publicKey, string environment, ILogger<SendGridVerifier> logger)
    {
        _enabled = enabled;
        _publicKey = publicKey;
        _environment = environment;
        _logger = logger;
    }

    public (bool Verified, bool Skipped, string? Reason) Verify(string rawBody, string? signature, string? timestamp)
    {
        if (!_enabled)
        {
            _logger.LogWarning(
                "SendGrid webhook verification is disabled; request rejected. " +
                "Set SENDGRID_WEBHOOK_VERIFICATION_ENABLED=true and configure " +
                "SENDGRID_WEBHOOK_PUBLIC_KEY to accept webhook callbacks.");
            return (false, false, "verification_disabled");
        }

        if (string.IsNullOrEmpty(_publicKey))
        {
            _logger.LogError("SendGrid webhook verification is enabled but no public key is configured");
            return (false, false, "missing_public_key");
        }

        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp))
        {
            _logger.LogWarning("SendGrid webhook request missing signature or timestamp headers");
            return (false, false, "missing_headers");
        }

        try
        {
            var payload = Encoding.UTF8.GetBytes(timestamp + rawBody);
            var publicKeyBytes = Convert.FromBase64String(_publicKey);

            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

            var signatureBytes = Convert.FromBase64String(signature);
            var valid = ecdsa.VerifyData(payload, signatureBytes, HashAlgorithmName.SHA256);

            if (!valid)
            {
                _logger.LogWarning("SendGrid webhook signature verification failed");
                return (false, false, "invalid_signature");
            }

            return (true, false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendGrid webhook verification threw an error");
            return (false, false, $"verification_error: {ex.Message}");
        }
    }
}
