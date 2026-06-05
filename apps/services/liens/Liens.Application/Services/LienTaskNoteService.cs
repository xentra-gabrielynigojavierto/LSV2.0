using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

/// <summary>
/// TASK-B04 — LienTaskNoteService post-cutover: all note runtime is delegated to the
/// canonical Task service via <see cref="ILiensTaskServiceClient"/>.
/// </summary>
public sealed class LienTaskNoteService : ILienTaskNoteService
{
    private readonly ILiensTaskServiceClient         _taskClient;
    private readonly IAuditPublisher                 _audit;
    private readonly ILogger<LienTaskNoteService>    _logger;

    public LienTaskNoteService(
        ILiensTaskServiceClient      taskClient,
        IAuditPublisher              audit,
        ILogger<LienTaskNoteService> logger)
    {
        _taskClient = taskClient;
        _audit      = audit;
        _logger     = logger;
    }

    public Task<List<TaskNoteResponse>> GetNotesAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default)
        => _taskClient.GetNotesAsync(tenantId, taskId, ct);

    public async Task<TaskNoteResponse> CreateNoteAsync(
        Guid                  tenantId,
        Guid                  taskId,
        Guid                  actorUserId,
        CreateTaskNoteRequest request,
        CancellationToken     ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]>
                { ["Content"] = ["Note content is required."] });

        if (request.Content.Length > 5000)
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]>
                { ["Content"] = ["Note content must not exceed 5000 characters."] });

        var result = await _taskClient.AddNoteAsync(
            tenantId, taskId, actorUserId, request.Content, request.CreatedByName, ct);

        _logger.LogInformation(
            "Note created via Task service: NoteId={NoteId} TaskId={TaskId}", result.Id, taskId);

        _audit.Publish(
            eventType:   "liens.task_note.created",
            action:      "create",
            description: "Note added to task",
            tenantId:    tenantId,
            actorUserId: actorUserId,
            entityType:  "LienTaskNote",
            entityId:    result.Id.ToString(),
            metadata:    $"taskId={taskId}");

        return result;
    }

    public async Task<TaskNoteResponse> UpdateNoteAsync(
        Guid                  tenantId,
        Guid                  taskId,
        Guid                  noteId,
        Guid                  actorUserId,
        UpdateTaskNoteRequest request,
        CancellationToken     ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]>
                { ["Content"] = ["Note content is required."] });

        if (request.Content.Length > 5000)
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]>
                { ["Content"] = ["Note content must not exceed 5000 characters."] });

        var result = await _taskClient.UpdateNoteAsync(
            tenantId, taskId, noteId, actorUserId, request.Content, ct);

        _logger.LogInformation(
            "Note updated via Task service: NoteId={NoteId} TaskId={TaskId}", noteId, taskId);

        _audit.Publish(
            eventType:   "liens.task_note.updated",
            action:      "update",
            description: "Note updated on task",
            tenantId:    tenantId,
            actorUserId: actorUserId,
            entityType:  "LienTaskNote",
            entityId:    noteId.ToString(),
            metadata:    $"taskId={taskId}");

        return result;
    }

    public async Task DeleteNoteAsync(
        Guid              tenantId,
        Guid              taskId,
        Guid              noteId,
        Guid              actorUserId,
        CancellationToken ct = default)
    {
        await _taskClient.DeleteNoteAsync(tenantId, taskId, noteId, actorUserId, ct);

        _logger.LogInformation(
            "Note deleted via Task service: NoteId={NoteId} TaskId={TaskId}", noteId, taskId);

        _audit.Publish(
            eventType:   "liens.task_note.deleted",
            action:      "delete",
            description: "Note deleted from task",
            tenantId:    tenantId,
            actorUserId: actorUserId,
            entityType:  "LienTaskNote",
            entityId:    noteId.ToString(),
            metadata:    $"taskId={taskId}");
    }
}
