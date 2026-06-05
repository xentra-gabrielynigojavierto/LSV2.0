using Notifications.Api.Middleware;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;

namespace Notifications.Api.Endpoints;

public static class ContactEndpoints
{
    public static void MapContactEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/contacts").WithTags("Contacts");

        group.MapGet("/suppressions", async (HttpContext context, IContactSuppressionRepository repo, int? limit, int? offset) =>
        {
            var tenantId = context.GetTenantId();
            var items = await repo.GetByTenantAsync(tenantId, limit ?? 50, offset ?? 0);
            return Results.Ok(items.Select(MapSuppressionDto));
        });

        group.MapPost("/suppressions", async (HttpContext context, IContactSuppressionRepository repo, CreateContactSuppressionDto request) =>
        {
            var tenantId = context.GetTenantId();
            var suppression = new ContactSuppression
            {
                TenantId = tenantId,
                Channel = request.Channel,
                ContactValue = request.ContactValue,
                SuppressionType = request.SuppressionType,
                Reason = request.Reason,
                ExpiresAt = request.ExpiresAt,
                Notes = request.Notes,
                Source = "api",
                CreatedBy = "api"
            };
            var result = await repo.CreateAsync(suppression);
            return Results.Created($"/v1/contacts/suppressions/{result.Id}", MapSuppressionDto(result));
        });

        group.MapDelete("/suppressions/{id:guid}", async (HttpContext context, IContactSuppressionRepository repo, Guid id) =>
        {
            var tenantId = context.GetTenantId();
            var existing = await repo.GetByIdAsync(id);
            if (existing == null) return Results.NotFound();
            if (existing.TenantId != tenantId) return Results.NotFound();
            await repo.DeleteAsync(id);
            return Results.NoContent();
        });

        group.MapGet("/health", async (HttpContext context, IRecipientContactHealthRepository repo, string channel, string contactValue) =>
        {
            var tenantId = context.GetTenantId();
            var health = await repo.FindByContactAsync(tenantId, channel, contactValue);
            return health != null ? Results.Ok(health) : Results.NotFound();
        });
    }

    private static ContactSuppressionDto MapSuppressionDto(ContactSuppression s) => new()
    {
        Id = s.Id, TenantId = s.TenantId, Channel = s.Channel, ContactValue = s.ContactValue,
        SuppressionType = s.SuppressionType, Status = s.Status, Reason = s.Reason,
        Source = s.Source, ExpiresAt = s.ExpiresAt, CreatedBy = s.CreatedBy, Notes = s.Notes,
        CreatedAt = s.CreatedAt, UpdatedAt = s.UpdatedAt
    };
}
