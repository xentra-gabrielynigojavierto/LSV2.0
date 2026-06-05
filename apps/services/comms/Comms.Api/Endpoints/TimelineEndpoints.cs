using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain;

namespace Comms.Api.Endpoints;

public static class TimelineEndpoints
{
    public static void MapTimelineEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/comms/conversations")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode);

        group.MapGet("/{id:guid}/timeline", GetTimeline)
            .RequirePermission(CommsPermissions.OperationalRead);
    }

    private static async Task<IResult> GetTimeline(
        Guid id,
        IConversationTimelineService timelineService,
        IParticipantRepository participantRepo,
        IConversationRepository conversationRepo,
        ICurrentRequestContext ctx,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? eventTypes = null,
        bool includeInternal = true,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var userId = ctx.UserId
            ?? throw new UnauthorizedAccessException("User context is required.");

        var conversation = await conversationRepo.GetByIdAsync(tenantId, id, ct);
        if (conversation is null)
            return Results.NotFound(new { error = "Conversation not found." });

        var participant = await participantRepo.GetActiveByUserIdAsync(tenantId, id, userId, ct);
        if (participant is null)
            return Results.Forbid();

        var isExternal = participant.ParticipantType == Domain.Enums.ParticipantType.ExternalContact;
        var effectiveIncludeInternal = includeInternal && !isExternal;

        var eventTypesList = string.IsNullOrWhiteSpace(eventTypes)
            ? null
            : eventTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var query = new TimelineQuery(fromDate, toDate, eventTypesList, effectiveIncludeInternal, page, pageSize);
        var result = await timelineService.GetTimelineAsync(tenantId, id, query, ct);
        return Results.Ok(result);
    }
}
