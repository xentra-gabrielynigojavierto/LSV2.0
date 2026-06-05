using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IReferralAttachmentRepository
{
    Task<List<ReferralAttachment>> GetByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
    Task AddAsync(ReferralAttachment attachment, CancellationToken ct = default);
}
