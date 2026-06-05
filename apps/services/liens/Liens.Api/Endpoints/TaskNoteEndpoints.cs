using System.Security.Claims;
using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;

namespace Liens.Api.Endpoints;

/// <summary>
/// LS-LIENS-FLOW-004 — Task Notes + History endpoints.
/// Provides per-task notes (text-only collaboration) and change history.
/// </summary>
public static class TaskNoteEndpoints
{
    public static void MapTaskNoteEndpoints(this WebApplication app)
    {
        var notesGroup = app.MapGroup("/api/liens/tasks/{taskId:guid}/notes")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .WithTags("TaskNotes");

        notesGroup.MapGet("/", GetNotes)
            .RequirePermission(LiensPermissions.TaskRead);

        notesGroup.MapPost("/", CreateNote)
            .RequirePermission(LiensPermissions.TaskNoteManage);

        notesGroup.MapPut("/{noteId:guid}", UpdateNote)
            .RequirePermission(LiensPermissions.TaskNoteManage);

        notesGroup.MapDelete("/{noteId:guid}", DeleteNote)
            .RequirePermission(LiensPermissions.TaskNoteManage);

        app.MapGet("/api/liens/tasks/{taskId:guid}/history", GetTaskHistory)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .RequirePermission(LiensPermissions.TaskRead)
            .WithTags("TaskNotes");
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async Task<IResult> GetNotes(
        Guid taskId,
        ILienTaskNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var notes = await svc.GetNotesAsync(tenantId, taskId, ct);
        return Results.Ok(notes);
    }

    private static async Task<IResult> CreateNote(
        Guid taskId,
        CreateTaskNoteRequest request,
        ILienTaskNoteService svc,
        ICurrentRequestContext ctx,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantId   = RequireTenantId(ctx);
        var userId     = RequireUserId(ctx);
        var authorName = user.FindFirstValue(ClaimTypes.Name)
                      ?? user.FindFirstValue("name")
                      ?? ctx.Email
                      ?? string.Empty;
        var enriched = new CreateTaskNoteRequest
        {
            Content         = request.Content,
            CreatedByName   = authorName,
        };
        var note = await svc.CreateNoteAsync(tenantId, taskId, userId, enriched, ct);
        return Results.Created($"/api/liens/tasks/{taskId}/notes/{note.Id}", note);
    }

    private static async Task<IResult> UpdateNote(
        Guid taskId,
        Guid noteId,
        UpdateTaskNoteRequest request,
        ILienTaskNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var note     = await svc.UpdateNoteAsync(tenantId, taskId, noteId, userId, request, ct);
        return Results.Ok(note);
    }

    private static async Task<IResult> DeleteNote(
        Guid taskId,
        Guid noteId,
        ILienTaskNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        await svc.DeleteNoteAsync(tenantId, taskId, noteId, userId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetTaskHistory(
        Guid taskId,
        ILiensTaskServiceClient taskClient,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var history  = await taskClient.GetHistoryAsync(tenantId, taskId, ct);
        return Results.Ok(history);
    }
}
