using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILienTaskNoteRepository
{
    Task<List<LienTaskNote>> GetByTaskIdAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
    Task<LienTaskNote?> GetByIdAsync(Guid tenantId, Guid noteId, CancellationToken ct = default);
    Task AddAsync(LienTaskNote note, CancellationToken ct = default);
    Task UpdateAsync(LienTaskNote note, CancellationToken ct = default);
}
