using Notifications.Api.Middleware;
using Notifications.Application.Constants;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

public static class ProviderEndpoints
{
    // Platform-default routing priority (mirrors ProviderRoutingService.PlatformProviderPriority)
    private static readonly Dictionary<string, string[]> PlatformPriority = new(StringComparer.OrdinalIgnoreCase)
    {
        ["email"] = new[] { "sendgrid", "smtp" },
        ["sms"]   = new[] { "twilio" },
        ["push"]  = Array.Empty<string>(),
    };

    private static readonly Dictionary<string, string> ProviderDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sendgrid"] = "SendGrid",
        ["smtp"]     = "SMTP",
        ["twilio"]   = "Twilio",
    };

    public static void MapProviderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/providers").WithTags("Providers");

        // ── Catalog ────────────────────────────────────────────────────────────────
        // Static list of all provider types supported by the platform.
        // No auth / tenant scope needed — this is purely informational.
        group.MapGet("/catalog", () =>
        {
            var catalog = PlatformPriority
                .SelectMany(kv => kv.Value.Select(providerType => new
                {
                    providerType,
                    channel     = kv.Key,
                    displayName = ProviderDisplayNames.TryGetValue(providerType, out var name) ? name : providerType,
                }))
                .ToArray();

            return Results.Ok(catalog);
        });

        // ── Configs ────────────────────────────────────────────────────────────────

        // When called without a tenant context (platform-admin), return the
        // platform-level provider configs stored under the sentinel TenantId.
        // Tenant calls still receive their own configs as before.
        group.MapGet("/configs", async (HttpContext context, ITenantProviderConfigService service, string? channel) =>
        {
            var tenantId = context.TryGetTenantId();
            var result = tenantId.HasValue
                ? await service.ListAsync(tenantId.Value, channel)
                : await service.ListPlatformAsync(channel);
            return Results.Ok(result);
        });

        group.MapGet("/configs/{id:guid}", async (HttpContext context, ITenantProviderConfigService service, Guid id) =>
        {
            var tenantId = context.TryGetTenantId();

            if (tenantId == null)
            {
                var dto = await service.GetPlatformByIdAsync(id);
                return dto != null ? Results.Ok(dto) : Results.NotFound();
            }

            var result = await service.GetByIdAsync(tenantId.Value, id);
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        group.MapPost("/configs", async (HttpContext context, ITenantProviderConfigService service, CreateTenantProviderConfigDto request) =>
        {
            var tenantId = context.TryGetTenantId();
            var result = tenantId.HasValue
                ? await service.CreateAsync(tenantId.Value, request)
                : await service.CreatePlatformAsync(request);
            return Results.Created($"/v1/providers/configs/{result.Id}", result);
        });

        group.MapPut("/configs/{id:guid}", async (HttpContext context, ITenantProviderConfigService service, Guid id, UpdateTenantProviderConfigDto request) =>
        {
            var tenantId = context.TryGetTenantId();
            var result = tenantId.HasValue
                ? await service.UpdateAsync(tenantId.Value, id, request)
                : await service.UpdatePlatformAsync(id, request);
            return Results.Ok(result);
        });

        group.MapDelete("/configs/{id:guid}", async (HttpContext context, ITenantProviderConfigService service, Guid id) =>
        {
            var tenantId = context.TryGetTenantId();
            if (tenantId.HasValue)
                await service.DeleteAsync(tenantId.Value, id);
            else
                await service.DeletePlatformAsync(id);
            return Results.NoContent();
        });

        group.MapPost("/configs/{id:guid}/validate", async (HttpContext context, ITenantProviderConfigService service, Guid id) =>
        {
            var tenantId = context.TryGetTenantId();
            var effectiveTenantId = tenantId ?? PlatformProvider.PlatformTenantId;

            try
            {
                await service.ValidateAsync(effectiveTenantId, id);
                return Results.Ok(new { data = new { valid = true, errors = Array.Empty<string>() } });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (Exception ex)
            {
                return Results.Ok(new { data = new { valid = false, errors = new[] { ex.Message } } });
            }
        });

        group.MapPost("/configs/{id:guid}/health-check", async (HttpContext context, ITenantProviderConfigService service, Guid id) =>
        {
            var tenantId = context.TryGetTenantId();
            var effectiveTenantId = tenantId ?? PlatformProvider.PlatformTenantId;

            try
            {
                var result = await service.HealthCheckAsync(effectiveTenantId, id);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        // ── Provider test ──────────────────────────────────────────────────────────
        // Sends a real message through the specified provider config so platform
        // admins can verify end-to-end outbound delivery.
        // For SMS: builds a fresh TwilioAdapter from the config's stored credentials
        // (CredentialsJson / SettingsJson) so the test uses the actual saved keys,
        // not whatever the globally-registered adapter was initialised with.
        group.MapPost("/configs/{id:guid}/test", async (
            HttpContext context,
            ITenantProviderConfigRepository repo,
            IEmailProviderAdapter emailAdapter,
            ITwilioAdapterFactory twilioAdapterFactory,
            Guid id,
            TestProviderRequest? request) =>
        {
            var tenantId = context.TryGetTenantId();

            var config = await repo.GetByIdAsync(id);
            if (config == null) return Results.NotFound(new { error = "Provider config not found." });

            // Allow access: must be the caller's own tenant config OR a platform config.
            var isPlatform  = config.TenantId == PlatformProvider.PlatformTenantId;
            var isOwnTenant = tenantId.HasValue && config.TenantId == tenantId.Value;
            if (!isPlatform && !isOwnTenant)
                return Results.Forbid();

            if (config.Channel.Equals("email", StringComparison.OrdinalIgnoreCase))
            {
                var toEmail = request?.ToEmail;
                if (string.IsNullOrWhiteSpace(toEmail))
                    return Results.BadRequest(new { error = "toEmail is required for email provider test." });

                var result = await emailAdapter.SendAsync(new EmailSendPayload
                {
                    To      = toEmail,
                    Subject = request?.Subject ?? "Test message from LegalSynq Platform",
                    Body    = request?.Body    ?? "This is a test notification sent from the LegalSynq platform admin panel.",
                    Html    = request?.Body,
                });

                return Results.Ok(new
                {
                    data = new
                    {
                        success = result.Success,
                        message = result.Success
                            ? $"Test email sent to {toEmail}."
                            : result.Failure?.Message ?? "Send failed.",
                    }
                });
            }

            if (config.Channel.Equals("sms", StringComparison.OrdinalIgnoreCase))
            {
                var toPhone = request?.ToPhone;
                if (string.IsNullOrWhiteSpace(toPhone))
                    return Results.BadRequest(new { error = "toPhone is required for SMS provider test." });

                // Build an adapter directly from the stored credentials so the test
                // exercises the exact config the user saved — not the global default.
                ISmsProviderAdapter smsAdapter;
                try
                {
                    smsAdapter = twilioAdapterFactory.CreateFromConfig(config);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Ok(new
                    {
                        data = new { success = false, message = $"Provider config error: {ex.Message}" }
                    });
                }

                var result = await smsAdapter.SendAsync(new SmsSendPayload
                {
                    To   = toPhone,
                    Body = request?.Body ?? "Test SMS from LegalSynq Platform.",
                });

                return Results.Ok(new
                {
                    data = new
                    {
                        success           = result.Success,
                        providerMessageId = result.ProviderMessageId,
                        message           = result.Success
                            ? $"Test SMS accepted by Twilio (SID: {result.ProviderMessageId}). If not received, check your Twilio console for delivery status — trial accounts can only send to verified numbers."
                            : result.Failure?.Message ?? "Send failed.",
                    }
                });
            }

            return Results.Ok(new { data = new { success = false, message = $"Test not supported for channel '{config.Channel}'." } });
        });

        // ── Channel settings ───────────────────────────────────────────────────────

        group.MapGet("/channel-settings", async (HttpContext context, ITenantChannelProviderSettingRepository repo) =>
        {
            var tenantId = context.TryGetTenantId();
            var settings = tenantId.HasValue
                ? await repo.GetByTenantAsync(tenantId.Value)
                : Enumerable.Empty<Domain.TenantChannelProviderSetting>();

            var response = settings.Select(s =>
            {
                // Resolve human-readable provider names from the platform priority list
                // when the channel operates in platform-managed mode.
                PlatformPriority.TryGetValue(s.Channel, out var priorityList);
                var primaryProvider  = priorityList is { Length: > 0 } ? priorityList[0] : null;
                var fallbackProvider = priorityList is { Length: > 1 } ? priorityList[1] : null;

                return new
                {
                    id                             = s.Id,
                    tenantId                       = s.TenantId,
                    channel                        = s.Channel,
                    mode                           = s.ProviderMode,   // alias expected by UI
                    providerMode                   = s.ProviderMode,
                    primaryProvider,
                    fallbackProvider,
                    primaryTenantProviderConfigId  = s.PrimaryTenantProviderConfigId,
                    fallbackTenantProviderConfigId = s.FallbackTenantProviderConfigId,
                    allowPlatformFallback          = s.AllowPlatformFallback,
                    allowAutomaticFailover         = s.AllowAutomaticFailover,
                    createdAt                      = s.CreatedAt,
                    updatedAt                      = s.UpdatedAt,
                };
            });

            return Results.Ok(response);
        });

        group.MapPut("/channel-settings/{channel}", async (HttpContext context, ITenantChannelProviderSettingRepository repo, string channel, UpdateChannelSettingDto request) =>
        {
            var tenantId = context.GetTenantId();
            var setting = new Domain.TenantChannelProviderSetting
            {
                TenantId = tenantId, Channel = channel,
                ProviderMode = request.ProviderMode ?? "platform_managed",
                PrimaryTenantProviderConfigId = request.PrimaryTenantProviderConfigId,
                FallbackTenantProviderConfigId = request.FallbackTenantProviderConfigId,
                AllowPlatformFallback = request.AllowPlatformFallback ?? true,
                AllowAutomaticFailover = request.AllowAutomaticFailover ?? true
            };
            var result = await repo.UpsertAsync(setting);
            return Results.Ok(result);
        });
    }
}

/// <summary>Payload for the provider test endpoint.</summary>
public class TestProviderRequest
{
    public string? ToEmail { get; set; }
    public string? ToPhone { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
}
