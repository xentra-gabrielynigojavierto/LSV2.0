using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;

namespace Liens.Api.Endpoints;

/// <summary>
/// LS-LIENS-CASE-005 — Case Notes endpoints.
/// Provides per-case notes with category, pin/unpin, and audit trail.
/// </summary>
public static class CaseNoteEndpoints
{
    public static void MapCaseNoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/cases/{caseId:guid}/notes")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .WithTags("CaseNotes");

        group.MapGet("/", GetNotes)
            .RequirePermission(LiensPermissions.CaseRead);

        group.MapPost("/", CreateNote)
            .RequirePermission(LiensPermissions.CaseNoteManage);

        group.MapPut("/{noteId:guid}", UpdateNote)
            .RequirePermission(LiensPermissions.CaseNoteManage);

        group.MapDelete("/{noteId:guid}", DeleteNote)
            .RequirePermission(LiensPermissions.CaseNoteManage);

        group.MapPost("/{noteId:guid}/pin", PinNote)
            .RequirePermission(LiensPermissions.CaseNoteManage);

        group.MapPost("/{noteId:guid}/unpin", UnpinNote)
            .RequirePermission(LiensPermissions.CaseNoteManage);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async Task<IResult> GetNotes(
        Guid caseId,
        ILienCaseNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var notes    = await svc.GetNotesAsync(tenantId, caseId, ct);
        return Results.Ok(notes);
    }

    private static async Task<IResult> CreateNote(
        Guid caseId,
        CreateCaseNoteRequest request,
        ILienCaseNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var note     = await svc.CreateNoteAsync(tenantId, caseId, userId, request, ct);
        return Results.Created($"/api/liens/cases/{caseId}/notes/{note.Id}", note);
    }

    private static async Task<IResult> UpdateNote(
        Guid caseId,
        Guid noteId,
        UpdateCaseNoteRequest request,
        ILienCaseNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var note     = await svc.UpdateNoteAsync(tenantId, caseId, noteId, userId, request, ct);
        return Results.Ok(note);
    }

    private static async Task<IResult> DeleteNote(
        Guid caseId,
        Guid noteId,
        ILienCaseNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        await svc.DeleteNoteAsync(tenantId, caseId, noteId, userId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> PinNote(
        Guid caseId,
        Guid noteId,
        ILienCaseNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var note     = await svc.PinNoteAsync(tenantId, caseId, noteId, userId, ct);
        return Results.Ok(note);
    }

    private static async Task<IResult> UnpinNote(
        Guid caseId,
        Guid noteId,
        ILienCaseNoteService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var note     = await svc.UnpinNoteAsync(tenantId, caseId, noteId, userId, ct);
        return Results.Ok(note);
    }
}
