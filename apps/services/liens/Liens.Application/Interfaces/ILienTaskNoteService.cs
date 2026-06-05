using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ILienTaskNoteService
{
    Task<List<TaskNoteResponse>> GetNotesAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
    Task<TaskNoteResponse> CreateNoteAsync(Guid tenantId, Guid taskId, Guid actorUserId, CreateTaskNoteRequest request, CancellationToken ct = default);
    Task<TaskNoteResponse> UpdateNoteAsync(Guid tenantId, Guid taskId, Guid noteId, Guid actorUserId, UpdateTaskNoteRequest request, CancellationToken ct = default);
    Task DeleteNoteAsync(Guid tenantId, Guid taskId, Guid noteId, Guid actorUserId, CancellationToken ct = default);
}
