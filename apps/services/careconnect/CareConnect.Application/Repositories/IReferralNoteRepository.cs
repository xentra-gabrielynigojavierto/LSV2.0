using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IReferralNoteRepository
{
    Task<ReferralNote?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<List<ReferralNote>> GetByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
    Task AddAsync(ReferralNote note, CancellationToken ct = default);
    Task UpdateAsync(ReferralNote note, CancellationToken ct = default);
}
