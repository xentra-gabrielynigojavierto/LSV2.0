using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Providers.Adapters;

public class SmtpAdapter : IEmailProviderAdapter
{
    public string ProviderType => "smtp";

    private readonly string _host;
    private readonly int _port;
    private readonly bool _useSsl;
    private readonly string _username;
    private readonly string _password;
    private readonly string _fromEmail;
    private readonly string? _fromName;
    private readonly ILogger<SmtpAdapter> _logger;

    public SmtpAdapter(string host, int port, bool useSsl, string username, string password, string fromEmail, string? fromName, ILogger<SmtpAdapter> logger)
    {
        _host = host; _port = port; _useSsl = useSsl;
        _username = username; _password = password;
        _fromEmail = fromEmail; _fromName = fromName;
        _logger = logger;
    }

    public Task<bool> ValidateConfigAsync()
        => Task.FromResult(!string.IsNullOrEmpty(_host) && _port > 0 && !string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password) && !string.IsNullOrEmpty(_fromEmail));

    public async Task<EmailSendResult> SendAsync(EmailSendPayload payload)
    {
        if (!await ValidateConfigAsync())
            return new EmailSendResult { Success = false, Failure = new ProviderFailure { Category = "auth_config_failure", Message = "SMTP config is incomplete", Retryable = false } };

        using var client = new SmtpClient();
        try
        {
            client.Timeout = 10000;
            await client.ConnectAsync(_host, _port, _useSsl ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable);
            await client.AuthenticateAsync(_username, _password);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_fromName ?? _fromEmail, _fromEmail));
            message.To.Add(MailboxAddress.Parse(payload.To));
            message.Subject = payload.Subject;
            if (payload.ReplyTo != null) message.ReplyTo.Add(MailboxAddress.Parse(payload.ReplyTo));

            var bodyBuilder = new BodyBuilder { TextBody = payload.Body };
            if (payload.Html != null) bodyBuilder.HtmlBody = payload.Html;
            message.Body = bodyBuilder.ToMessageBody();

            var result = await client.SendAsync(message);
            _logger.LogInformation("SMTP: email sent to {To}, messageId={MessageId}", payload.To, result);
            return new EmailSendResult { Success = true, ProviderMessageId = result };
        }
        catch (Exception ex)
        {
            var category = ClassifyError(ex);
            _logger.LogWarning(ex, "SMTP: send failed {Category}", category);
            return new EmailSendResult { Success = false, Failure = new ProviderFailure { Category = category, Message = ex.Message[..Math.Min(ex.Message.Length, 500)], Retryable = category is "retryable_provider_failure" or "provider_unavailable" } };
        }
        finally
        {
            if (client.IsConnected) await client.DisconnectAsync(true);
        }
    }

    public async Task<ProviderHealthResult> HealthCheckAsync()
    {
        if (!await ValidateConfigAsync()) return new ProviderHealthResult { Status = "down" };
        using var client = new SmtpClient();
        var start = DateTime.UtcNow;
        try
        {
            client.Timeout = 5000;
            await client.ConnectAsync(_host, _port, _useSsl ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable);
            await client.AuthenticateAsync(_username, _password);
            var latencyMs = (int)(DateTime.UtcNow - start).TotalMilliseconds;
            return new ProviderHealthResult { Status = "healthy", LatencyMs = latencyMs };
        }
        catch (Exception ex)
        {
            var latencyMs = (int)(DateTime.UtcNow - start).TotalMilliseconds;
            var category = ClassifyError(ex);
            return new ProviderHealthResult { Status = category == "auth_config_failure" ? "down" : "degraded", LatencyMs = latencyMs };
        }
        finally
        {
            if (client.IsConnected) await client.DisconnectAsync(true);
        }
    }

    private static string ClassifyError(Exception ex)
    {
        var msg = ex.Message.ToLowerInvariant();
        if (msg.Contains("auth") || msg.Contains("credential") || msg.Contains("535") || msg.Contains("534")) return "auth_config_failure";
        if (msg.Contains("timeout") || msg.Contains("refused") || msg.Contains("enotfound")) return "provider_unavailable";
        if (msg.Contains("550") || msg.Contains("invalid") || msg.Contains("recipient")) return "invalid_recipient";
        return "retryable_provider_failure";
    }
}
