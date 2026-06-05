using Support.Api.Data;
using Support.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Support.Api.Services;

public interface ITicketNumberGenerator
{
    Task<string> NextAsync(string tenantId, CancellationToken ct = default);
}

/// <summary>
/// Tenant-scoped support ticket number generator.
/// Format: SUP-{YYYY}-{000001}
/// Concurrency: serialized per (tenant, year) row via SaveChanges retry on
/// concurrency / unique constraint violation. Sufficient for MVP normal load.
/// </summary>
public class TicketNumberGenerator : ITicketNumberGenerator
{
    private readonly SupportDbContext _db;

    public TicketNumberGenerator(SupportDbContext db) => _db = db;

    public async Task<string> NextAsync(string tenantId, CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        const int maxAttempts = 10;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var seq = await _db.TicketNumberSequences
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Year == year, ct);

            long nextValue;
            if (seq is null)
            {
                seq = new TicketNumberSequence { TenantId = tenantId, Year = year, LastValue = 1, RowVersion = 1 };
                _db.TicketNumberSequences.Add(seq);
                nextValue = 1;
            }
            else
            {
                seq.LastValue += 1;
                seq.RowVersion += 1; // bump concurrency token
                nextValue = seq.LastValue;
            }

            try
            {
                await _db.SaveChangesAsync(ct);
                return $"SUP-{year}-{nextValue:D6}";
            }
            catch (DbUpdateConcurrencyException)
            {
                // another worker incremented this row first — retry with fresh read
                _db.ChangeTracker.Clear();
            }
            catch (DbUpdateException)
            {
                // unique key collision on first insert from a parallel worker — retry with fresh read
                _db.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException("Failed to allocate ticket number after retries.");
    }
}
