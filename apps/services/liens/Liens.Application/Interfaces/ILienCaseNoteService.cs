using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ILienCaseNoteService
{
    Task<List<CaseNoteResponse>> GetNotesAsync(
        Guid tenantId, Guid caseId, CancellationToken ct = default);

    Task<CaseNoteResponse> CreateNoteAsync(
        Guid tenantId, Guid caseId, Guid actorUserId,
        CreateCaseNoteRequest request, CancellationToken ct = default);

    Task<CaseNoteResponse> UpdateNoteAsync(
        Guid tenantId, Guid caseId, Guid noteId, Guid actorUserId,
        UpdateCaseNoteRequest request, CancellationToken ct = default);

    Task DeleteNoteAsync(
        Guid tenantId, Guid caseId, Guid noteId, Guid actorUserId,
        CancellationToken ct = default);

    Task<CaseNoteResponse> PinNoteAsync(
        Guid tenantId, Guid caseId, Guid noteId, Guid actorUserId,
        CancellationToken ct = default);

    Task<CaseNoteResponse> UnpinNoteAsync(
        Guid tenantId, Guid caseId, Guid noteId, Guid actorUserId,
        CancellationToken ct = default);
}
