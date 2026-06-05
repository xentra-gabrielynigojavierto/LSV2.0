using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comms.Infrastructure.Repositories;

public class ExternalParticipantIdentityRepository : IExternalParticipantIdentityRepository
{
    private readonly CommsDbContext _db;

    public ExternalParticipantIdentityRepository(CommsDbContext db)
    {
        _db = db;
    }

    public async Task<ExternalParticipantIdentity?> FindByEmailAsync(Guid tenantId, string normalizedEmail, CancellationToken ct = default)
    {
        return await _db.ExternalParticipantIdentities
            .Where(e => e.TenantId == tenantId && e.NormalizedEmail == normalizedEmail && e.IsActive)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(ExternalParticipantIdentity entity, CancellationToken ct = default)
    {
        await _db.ExternalParticipantIdentities.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ExternalParticipantIdentity entity, CancellationToken ct = default)
    {
        _db.ExternalParticipantIdentities.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
