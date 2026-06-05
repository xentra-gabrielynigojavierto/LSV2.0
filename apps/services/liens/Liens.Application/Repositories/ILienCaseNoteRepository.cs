using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILienCaseNoteRepository
{
    Task<List<LienCaseNote>> GetByCaseIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default);
    Task<LienCaseNote?> GetByIdAsync(Guid tenantId, Guid noteId, CancellationToken ct = default);
    Task AddAsync(LienCaseNote note, CancellationToken ct = default);
    Task UpdateAsync(LienCaseNote note, CancellationToken ct = default);
}
