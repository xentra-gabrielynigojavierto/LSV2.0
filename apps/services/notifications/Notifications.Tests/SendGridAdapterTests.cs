using Microsoft.Extensions.Logging.Abstractions;
using Notifications.Application.Interfaces;
using Notifications.Infrastructure.Providers.Adapters;
using System.Net;
using System.Net.Http;
using Xunit;

namespace Notifications.Tests;

/// <summary>
/// Verifies that <see cref="SendGridAdapter"/> maps SendGrid HTTP responses and
/// configuration failures to the correct <see cref="ProviderFailure.Category"/> values.
///
/// The four categories relevant to invite delivery:
///   auth_config_failure    — API key missing or rejected (401/403).
///   invalid_recipient      — malformed address (400 + "invalid email/recipient" body).
///   retryable_provider_failure — transient HTTP errors (429, 5xx).
///   provider_unavailable   — network timeout or connection refused.
/// </summary>
public class SendGridAdapterTests
{
    private static SendGridAdapter BuildAdapter(
        string apiKey,
        HttpMessageHandler? handler = null)
    {
        var http = handler != null
            ? new HttpClient(handler)
            : new HttpClient();

        return new SendGridAdapter(
            apiKey:           apiKey,
            defaultFromEmail: "noreply@example.com",
            defaultFromName:  "Test",
            http:             http,
            logger:           NullLogger<SendGridAdapter>.Instance);
    }

    private static EmailSendPayload SamplePayload() => new()
    {
        To      = "recipient@example.com",
        Subject = "Test",
        Body    = "Hello",
    };

    // ── Empty API key ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_Returns_AuthConfigFailure_WhenApiKeyIsEmpty()
    {
        var adapter = BuildAdapter(apiKey: "");

        var result = await adapter.SendAsync(SamplePayload());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("auth_config_failure", result.Failure!.Category);
        Assert.False(result.Failure.Retryable);
    }

    [Fact]
    public async Task SendAsync_Returns_AuthConfigFailure_WhenApiKeyIsWhitespace()
    {
        var adapter = BuildAdapter(apiKey: "   ");

        var result = await adapter.SendAsync(SamplePayload());

        Assert.False(result.Success);
        Assert.Equal("auth_config_failure", result.Failure!.Category);
    }

    // ── 401 / 403 responses ───────────────────────────────────────────────────

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task SendAsync_Returns_AuthConfigFailure_OnUnauthorizedResponse(int statusCode)
    {
        var handler = new StubHttpHandler(statusCode, "Unauthorized");
        var adapter = BuildAdapter(apiKey: "valid-key", handler: handler);

        var result = await adapter.SendAsync(SamplePayload());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("auth_config_failure", result.Failure!.Category);
        Assert.False(result.Failure.Retryable,
            "auth_config_failure should not be retryable — the API key won't become valid on its own.");
    }

    // ── 400 responses ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_Returns_InvalidRecipient_OnBadRequestWithInvalidEmailBody()
    {
        var handler = new StubHttpHandler(400, "{\"errors\":[{\"message\":\"The recipient email address is invalid\"}]}");
        var adapter = BuildAdapter(apiKey: "valid-key", handler: handler);

        var result = await adapter.SendAsync(SamplePayload());

        Assert.False(result.Success);
        Assert.Equal("invalid_recipient", result.Failure!.Category);
        Assert.False(result.Failure.Retryable);
    }

    [Fact]
    public async Task SendAsync_Returns_NonRetryableFailure_OnBadRequestWithGenericBody()
    {
        var handler = new StubHttpHandler(400, "{\"errors\":[{\"message\":\"Some other bad request\"}]}");
        var adapter = BuildAdapter(apiKey: "valid-key", handler: handler);

        var result = await adapter.SendAsync(SamplePayload());

        Assert.False(result.Success);
        Assert.Equal("non_retryable_failure", result.Failure!.Category);
        Assert.False(result.Failure.Retryable);
    }

    // ── Retryable responses ───────────────────────────────────────────────────

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task SendAsync_Returns_RetryableProviderFailure_OnTransientError(int statusCode)
    {
        var handler = new StubHttpHandler(statusCode, "Service unavailable");
        var adapter = BuildAdapter(apiKey: "valid-key", handler: handler);

        var result = await adapter.SendAsync(SamplePayload());

        Assert.False(result.Success);
        Assert.Equal("retryable_provider_failure", result.Failure!.Category);
        Assert.True(result.Failure.Retryable);
    }

    // ── Network timeout ───────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_Returns_ProviderUnavailable_OnOperationCanceledException()
    {
        var handler = new TimeoutHttpHandler();
        var adapter = BuildAdapter(apiKey: "valid-key", handler: handler);

        var result = await adapter.SendAsync(SamplePayload());

        Assert.False(result.Success);
        Assert.Equal("provider_unavailable", result.Failure!.Category);
        Assert.True(result.Failure.Retryable);
    }

    // ── Success ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_Returns_Success_On202Response()
    {
        var handler = new StubHttpHandler(202, "", messageId: "msg-abc-123");
        var adapter = BuildAdapter(apiKey: "valid-key", handler: handler);

        var result = await adapter.SendAsync(SamplePayload());

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("msg-abc-123", result.ProviderMessageId);
    }

    // ── HTTP stubs ────────────────────────────────────────────────────────────

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly int    _statusCode;
        private readonly string _body;
        private readonly string? _messageId;

        public StubHttpHandler(int statusCode, string body, string? messageId = null)
        {
            _statusCode = statusCode;
            _body       = body;
            _messageId  = messageId;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage((HttpStatusCode)_statusCode)
            {
                Content = new StringContent(_body),
            };

            if (_messageId != null)
                response.Headers.TryAddWithoutValidation("X-Message-Id", _messageId);

            return Task.FromResult(response);
        }
    }

    private sealed class TimeoutHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(
                new TaskCanceledException("Simulated timeout"));
    }
}
