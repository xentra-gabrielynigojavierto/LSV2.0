using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class ReferralAttachmentRepository : IReferralAttachmentRepository
{
    private readonly CareConnectDbContext _db;

    public ReferralAttachmentRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<List<ReferralAttachment>> GetByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default)
        => await _db.ReferralAttachments
            .Where(a => a.TenantId == tenantId && a.ReferralId == referralId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task AddAsync(ReferralAttachment attachment, CancellationToken ct = default)
    {
        await _db.ReferralAttachments.AddAsync(attachment, ct);
        await _db.SaveChangesAsync(ct);
    }
}
