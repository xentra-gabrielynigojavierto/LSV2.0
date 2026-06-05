// BLK-OBS-01: Added structured logging and audit event emission.
using BuildingBlocks.Authentication.ServiceTokens;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using AuditVisibility = LegalSynq.AuditClient.Enums.VisibilityScope;

namespace CareConnect.Api.Endpoints;

public static class InternalProvisionEndpoints
{
    public static IEndpointRouteBuilder MapInternalProvisionEndpoints(
        this IEndpointRouteBuilder routes)
    {
        // Requires a valid platform service token (HS256, FLOW_SERVICE_TOKEN_SECRET).
        // The gateway blocks /internal/** from external clients via the Deny policy on
        // careconnect-internal-block, so this route is only reachable by internal services.
        routes.MapPost("/internal/provision-provider", ProvisionProvider)
            .RequireAuthorization("ServiceOnly");

        return routes;
    }

    private static async Task<IResult> ProvisionProvider(
        ProvisionProviderRequest body,
        IProviderRepository      providers,
        IAuditEventClient        auditClient,
        ILoggerFactory           loggerFactory,
        HttpContext               http,
        CancellationToken        ct)
    {
        var logger = loggerFactory.CreateLogger("CareConnect.InternalProvision");

        if (body.TenantId == Guid.Empty)
            return Results.BadRequest(new { error = "tenantId is required." });
        if (body.OrganizationId == Guid.Empty)
            return Results.BadRequest(new { error = "organizationId is required." });
        if (string.IsNullOrWhiteSpace(body.ProviderName))
            return Results.BadRequest(new { error = "providerName is required." });

        var correlationId = http.Items["CorrelationId"]?.ToString() ?? http.TraceIdentifier;

        var existing = await providers.GetByOrganizationIdAsync(body.OrganizationId, ct);

        if (existing is not null)
        {
            if (!existing.IsActive || !existing.AcceptingReferrals)
            {
                existing.Activate();
                await providers.UpdateAsync(existing, ct);

                // BLK-OBS-01: log + audit reactivation
                logger.LogInformation(
                    "InternalProvision: Provider {ProviderId} reactivated. TenantId={TenantId} OrgId={OrgId} RequestId={RequestId}",
                    existing.Id, body.TenantId, body.OrganizationId, correlationId);

                _ = EmitAuditAsync(auditClient, logger,
                    eventType:     "careconnect.provider.provisioned",
                    action:        "Reactivated",
                    description:   $"Provider '{existing.Id}' (org '{body.OrganizationId}') reactivated via internal provision endpoint.",
                    tenantId:      body.TenantId,
                    providerId:    existing.Id,
                    correlationId: correlationId);
            }

            return Results.Ok(new ProvisionProviderResponse(existing.Id, IsNew: false));
        }

        var provider = Provider.Create(
            tenantId: body.TenantId,
            name: body.ProviderName.Trim(),
            organizationName: body.ProviderName.Trim(),
            email: "",
            phone: "",
            addressLine1: "",
            city: "",
            state: "",
            postalCode: "",
            isActive: true,
            acceptingReferrals: true,
            createdByUserId: null);

        provider.LinkOrganization(body.OrganizationId);
        await providers.AddAsync(provider, ct);

        // BLK-OBS-01: log + audit new provider creation
        logger.LogInformation(
            "InternalProvision: Provider {ProviderId} created. TenantId={TenantId} OrgId={OrgId} Name={Name} RequestId={RequestId}",
            provider.Id, body.TenantId, body.OrganizationId, body.ProviderName.Trim(), correlationId);

        _ = EmitAuditAsync(auditClient, logger,
            eventType:     "careconnect.provider.provisioned",
            action:        "Created",
            description:   $"Provider '{provider.Id}' (org '{body.OrganizationId}') created via internal provision endpoint for tenant '{body.TenantId}'.",
            tenantId:      body.TenantId,
            providerId:    provider.Id,
            correlationId: correlationId);

        return Results.Ok(new ProvisionProviderResponse(provider.Id, IsNew: true));
    }

    private static Task EmitAuditAsync(
        IAuditEventClient auditClient,
        ILogger           logger,
        string            eventType,
        string            action,
        string            description,
        Guid              tenantId,
        Guid              providerId,
        string            correlationId)
    {
        try
        {
            return auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = eventType,
                EventCategory = EventCategory.Business,
                SourceSystem  = "care-connect",
                SourceService = "internal-provision",
                Visibility    = AuditVisibility.Tenant,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                Scope = new AuditEventScopeDto
                {
                    ScopeType = ScopeType.Tenant,
                    TenantId  = tenantId.ToString(),
                },
                Actor = new AuditEventActorDto
                {
                    Type = ActorType.System,
                    Id   = "internal-provision",
                    Name = "InternalProvisionEndpoint",
                },
                Entity = new AuditEventEntityDto
                {
                    Type = "Provider",
                    Id   = providerId.ToString(),
                },
                Action        = action,
                Description   = description,
                Outcome       = "success",
                CorrelationId = correlationId,
                IdempotencyKey = IdempotencyKey.ForWithTimestamp(
                    DateTimeOffset.UtcNow, "care-connect", eventType, providerId.ToString()),
                Tags = ["provision", "internal", "provider"],
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "InternalProvision: Audit event emission failed for provider {ProviderId}.", providerId);
            return Task.CompletedTask;
        }
    }
}

public sealed record ProvisionProviderRequest(
    Guid TenantId,
    Guid OrganizationId,
    string ProviderName);

public sealed record ProvisionProviderResponse(
    Guid ProviderId,
    bool IsNew);
