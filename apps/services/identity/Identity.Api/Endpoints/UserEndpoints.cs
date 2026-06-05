using System.Security.Claims;
using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapPost("/api/users", async (
            ClaimsPrincipal   caller,
            CreateUserRequest request,
            IUserService      userService,
            IAuditEventClient auditClient,
            CancellationToken ct) =>
        {
            // Callers may only create users within their own tenant.
            var tenantIdStr = caller.FindFirstValue("tenant_id");
            if (!Guid.TryParse(tenantIdStr, out var callerTenantId))
                return Results.Unauthorized();

            if (request.TenantId != callerTenantId)
                return Results.Forbid();

            try
            {
                var user = await userService.CreateUserAsync(request, ct);

                // Canonical audit: identity.user.created — fire-and-observe.
                var now = DateTimeOffset.UtcNow;
                _ = auditClient.IngestAsync(new IngestAuditEventRequest
                {
                    EventType     = "identity.user.created",
                    EventCategory = EventCategory.Administrative,
                    SourceSystem  = "identity-service",
                    SourceService = "user-api",
                    Visibility    = VisibilityScope.Tenant,
                    Severity      = SeverityLevel.Info,
                    OccurredAtUtc = now,
                    Scope = new AuditEventScopeDto
                    {
                        ScopeType = ScopeType.Tenant,
                        TenantId  = user.TenantId.ToString(),
                    },
                    Actor = new AuditEventActorDto
                    {
                        Type = ActorType.System,
                        Name = "identity-service",
                    },
                    Entity = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
                    Action      = "UserCreated",
                    Description = $"User '{user.Email}' created in tenant {user.TenantId}.",
                    After       = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        userId = user.Id,
                        email  = user.Email,
                        tenantId = user.TenantId,
                    }),
                    IdempotencyKey = IdempotencyKey.For("identity-service", "identity.user.created", user.Id.ToString()),
                    Tags = ["user-management", "provisioning"],
                });

                return Results.Created($"/api/users/{user.Id}", user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        }).RequireAuthorization();

        app.MapGet("/api/users", async (
            ClaimsPrincipal   caller,
            IUserService      userService,
            IdentityDbContext db,
            CancellationToken ct) =>
        {
            var tenantIdStr = caller.FindFirstValue("tenant_id");
            if (!Guid.TryParse(tenantIdStr, out var tenantId))
                return Results.Unauthorized();

            var users = await userService.GetByTenantAsync(tenantId, ct);
            var userIds = users.Select(u => u.Id).ToList();

            var groupCounts = await db.AccessGroupMemberships
                .Where(am => userIds.Contains(am.UserId)
                          && am.MembershipStatus == MembershipStatus.Active)
                .GroupBy(am => am.UserId)
                .Select(g => new { userId = g.Key, count = g.Count() })
                .ToDictionaryAsync(x => x.userId, x => x.count, ct);

            var productCounts = await db.UserProductAccessRecords
                .Where(upa => userIds.Contains(upa.UserId)
                           && upa.AccessStatus == AccessStatus.Granted)
                .GroupBy(upa => upa.UserId)
                .Select(g => new { userId = g.Key, count = g.Count() })
                .ToDictionaryAsync(x => x.userId, x => x.count, ct);

            var pendingInviteeList = await db.UserInvitations
                .Where(inv => userIds.Contains(inv.UserId)
                           && inv.TenantId == tenantId
                           && inv.Status == UserInvitation.Statuses.Pending)
                .Select(inv => inv.UserId)
                .ToListAsync(ct);
            var pendingInvitees = new HashSet<Guid>(pendingInviteeList);

            var enriched = users.Select(u => new
            {
                id                = u.Id,
                tenantId          = u.TenantId,
                email             = u.Email,
                firstName         = u.FirstName,
                lastName          = u.LastName,
                isActive          = u.IsActive,
                status            = pendingInvitees.Contains(u.Id) ? "Invited"
                                  : u.IsActive                     ? "Active"
                                                                   : "Inactive",
                roles             = u.Roles,
                productRoles      = u.ProductRoles,
                groupCount        = groupCounts.GetValueOrDefault(u.Id, 0),
                productCount      = productCounts.GetValueOrDefault(u.Id, 0),
                avatarDocumentId  = u.AvatarDocumentId,
            });

            return Results.Ok(enriched);
        }).RequireAuthorization();

        app.MapGet("/api/users/{id:guid}", async (
            Guid              id,
            ClaimsPrincipal   caller,
            IUserService      userService,
            CancellationToken ct) =>
        {
            var tenantIdStr = caller.FindFirstValue("tenant_id");
            if (!Guid.TryParse(tenantIdStr, out var callerTenantId))
                return Results.Unauthorized();

            var user = await userService.GetByIdAsync(id, ct);
            if (user is null) return Results.NotFound();

            // Callers may only view users within their own tenant.
            if (user.TenantId != callerTenantId)
                return Results.Forbid();

            return Results.Ok(user);
        }).RequireAuthorization();
    }
}
