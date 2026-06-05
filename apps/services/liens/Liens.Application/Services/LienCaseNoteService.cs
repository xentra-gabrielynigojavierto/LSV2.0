using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class LienCaseNoteService : ILienCaseNoteService
{
    private readonly ICaseRepository          _caseRepo;
    private readonly ILienCaseNoteRepository  _noteRepo;
    private readonly IAuditPublisher          _audit;
    private readonly ILogger<LienCaseNoteService> _logger;

    public LienCaseNoteService(
        ICaseRepository          caseRepo,
        ILienCaseNoteRepository  noteRepo,
        IAuditPublisher          audit,
        ILogger<LienCaseNoteService> logger)
    {
        _caseRepo = caseRepo;
        _noteRepo = noteRepo;
        _audit    = audit;
        _logger   = logger;
    }

    public async Task<List<CaseNoteResponse>> GetNotesAsync(
        Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        await EnsureCaseExistsAsync(tenantId, caseId, ct);
        var notes = await _noteRepo.GetByCaseIdAsync(tenantId, caseId, ct);
        return notes.Select(MapToResponse).ToList();
    }

    public async Task<CaseNoteResponse> CreateNoteAsync(
        Guid tenantId, Guid caseId, Guid actorUserId,
        CreateCaseNoteRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]> { ["Content"] = ["Note content is required."] });
        if (request.Content.Length > 5000)
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]> { ["Content"] = ["Note content must not exceed 5000 characters."] });

        await EnsureCaseExistsAsync(tenantId, caseId, ct);

        var note = LienCaseNote.Create(
            caseId:          caseId,
            tenantId:        tenantId,
            content:         request.Content,
            category:        request.Category,
            createdByUserId: actorUserId,
            createdByName:   string.IsNullOrWhiteSpace(request.CreatedByName) ? "Unknown" : request.CreatedByName.Trim());

        await _noteRepo.AddAsync(note, ct);

        _logger.LogInformation("Case note created: NoteId={NoteId} CaseId={CaseId}", note.Id, caseId);

        _audit.Publish(
            eventType:   "liens.case_note.created",
            action:      "create",
            description: "Note added to case",
            tenantId:    tenantId,
            actorUserId: actorUserId,
            entityType:  "LienCaseNote",
            entityId:    note.Id.ToString(),
            metadata:    $"caseId={caseId}");

        _audit.Publish(
            eventType:   "liens.case.note_added",
            action:      "update",
            description: $"Note added to case by {note.CreatedByName}",
            tenantId:    tenantId,
            actorUserId: actorUserId,
            entityType:  "Case",
            entityId:    caseId.ToString(),
            metadata:    $"noteId={note.Id}");

        return MapToResponse(note);
    }

    public async Task<CaseNoteResponse> UpdateNoteAsync(
        Guid tenantId, Guid caseId, Guid noteId, Guid actorUserId,
        UpdateCaseNoteRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]> { ["Content"] = ["Note content is required."] });
        if (request.Content.Length > 5000)
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]> { ["Content"] = ["Note content must not exceed 5000 characters."] });

        await EnsureCaseExistsAsync(tenantId, caseId, ct);

        var note = await _noteRepo.GetByIdAsync(tenantId, noteId, ct)
            ?? throw new NotFoundException($"Note '{noteId}' not found.");

        if (note.CaseId != caseId)
            throw new NotFoundException($"Note '{noteId}' does not belong to case '{caseId}'.");

        if (note.IsDeleted)
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]> { ["Note"] = ["Cannot edit a deleted note."] });

        if (note.CreatedByUserId != actorUserId)
            throw new UnauthorizedAccessException("You can only edit your own notes.");

        note.Edit(request.Content, request.Category, actorUserId);
        await _noteRepo.UpdateAsync(note, ct);

        _logger.LogInformation("Case note updated: NoteId={NoteId} CaseId={CaseId}", noteId, caseId);

        _audit.Publish(
            eventType:   "liens.case_note.updated",
            action:      "update",
            description: "Note updated on case",
            tenantId:    tenantId,
            actorUserId: actorUserId,
            entityType:  "LienCaseNote",
            entityId:    noteId.ToString(),
            metadata:    $"caseId={caseId}");

        return MapToResponse(note);
    }

    public async Task DeleteNoteAsync(
        Guid tenantId, Guid caseId, Guid noteId, Guid actorUserId,
        CancellationToken ct = default)
    {
        await EnsureCaseExistsAsync(tenantId, caseId, ct);

        var note = await _noteRepo.GetByIdAsync(tenantId, noteId, ct)
            ?? throw new NotFoundException($"Note '{noteId}' not found.");

        if (note.CaseId != caseId)
            throw new NotFoundException($"Note '{noteId}' does not belong to case '{caseId}'.");

        if (note.IsDeleted) return;

        if (note.CreatedByUserId != actorUserId)
            throw new UnauthorizedAccessException("You can only delete your own notes.");

        note.SoftDelete();
        await _noteRepo.UpdateAsync(note, ct);

        _logger.LogInformation("Case note deleted: NoteId={NoteId} CaseId={CaseId}", noteId, caseId);

        _audit.Publish(
            eventType:   "liens.case_note.deleted",
            action:      "delete",
            description: "Note deleted from case",
            tenantId:    tenantId,
            actorUserId: actorUserId,
            entityType:  "LienCaseNote",
            entityId:    noteId.ToString(),
            metadata:    $"caseId={caseId}");
    }

    public async Task<CaseNoteResponse> PinNoteAsync(
        Guid tenantId, Guid caseId, Guid noteId, Guid actorUserId,
        CancellationToken ct = default)
    {
        await EnsureCaseExistsAsync(tenantId, caseId, ct);

        var note = await _noteRepo.GetByIdAsync(tenantId, noteId, ct)
            ?? throw new NotFoundException($"Note '{noteId}' not found.");

        if (note.CaseId != caseId)
            throw new NotFoundException($"Note '{noteId}' does not belong to case '{caseId}'.");

        if (note.IsDeleted)
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]> { ["Note"] = ["Cannot pin a deleted note."] });

        note.Pin();
        await _noteRepo.UpdateAsync(note, ct);

        _audit.Publish(
            eventType:   "liens.case_note.pinned",
            action:      "update",
            description: "Note pinned on case",
            tenantId:    tenantId,
            actorUserId: actorUserId,
            entityType:  "LienCaseNote",
            entityId:    noteId.ToString(),
            metadata:    $"caseId={caseId}");

        return MapToResponse(note);
    }

    public async Task<CaseNoteResponse> UnpinNoteAsync(
        Guid tenantId, Guid caseId, Guid noteId, Guid actorUserId,
        CancellationToken ct = default)
    {
        await EnsureCaseExistsAsync(tenantId, caseId, ct);

        var note = await _noteRepo.GetByIdAsync(tenantId, noteId, ct)
            ?? throw new NotFoundException($"Note '{noteId}' not found.");

        if (note.CaseId != caseId)
            throw new NotFoundException($"Note '{noteId}' does not belong to case '{caseId}'.");

        if (note.IsDeleted)
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]> { ["Note"] = ["Cannot unpin a deleted note."] });

        note.Unpin();
        await _noteRepo.UpdateAsync(note, ct);

        _audit.Publish(
            eventType:   "liens.case_note.unpinned",
            action:      "update",
            description: "Note unpinned on case",
            tenantId:    tenantId,
            actorUserId: actorUserId,
            entityType:  "LienCaseNote",
            entityId:    noteId.ToString(),
            metadata:    $"caseId={caseId}");

        return MapToResponse(note);
    }

    private async Task EnsureCaseExistsAsync(Guid tenantId, Guid caseId, CancellationToken ct)
    {
        var c = await _caseRepo.GetByIdAsync(tenantId, caseId, ct);
        if (c is null)
            throw new NotFoundException($"Case '{caseId}' not found for tenant '{tenantId}'.");
    }

    private static CaseNoteResponse MapToResponse(LienCaseNote n) => new()
    {
        Id              = n.Id,
        CaseId          = n.CaseId,
        Content         = n.Content,
        Category        = n.Category,
        IsPinned        = n.IsPinned,
        CreatedByUserId = n.CreatedByUserId,
        CreatedByName   = n.CreatedByName,
        IsEdited        = n.IsEdited,
        CreatedAtUtc    = n.CreatedAtUtc,
        UpdatedAtUtc    = n.UpdatedAtUtc,
    };
}
