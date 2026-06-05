// LSCC-01-005: Referral performance metrics service — DB loading layer.
//
// Separates DB concerns from calculation logic (see ReferralPerformanceCalculator).
//
// Data loading strategy (bounded to keep queries fast):
//   1. Load referrals in window (CreatedAtUtc >= since), eager-load Provider
//   2. Collect the referral IDs from step 1
//   3. Load the earliest ReferralStatusHistory entry per referral where NewStatus=="Accepted"
//   4. Load all currently-New referrals for the aging distribution (no window filter)
//   5. Build RawReferralRecord list, pass to ReferralPerformanceCalculator.Compute()
//
// AcceptedAt derivation:
//   - Earliest ChangedAtUtc in ReferralStatusHistory where NewStatus=="Accepted" for that referral
//   - "Earliest" chosen to avoid double-counting when a referral is re-opened and re-accepted
//   - If no history entry exists the referral is treated as non-accepted for TTA purposes only;
//     it still contributes to total and acceptance rate if Status=="Accepted"
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CareConnect.Infrastructure.Services;

public sealed class ReferralPerformanceService : IReferralPerformanceService
{
    private readonly CareConnectDbContext                      _db;
    private readonly ILogger<ReferralPerformanceService>       _logger;

    public ReferralPerformanceService(
        CareConnectDbContext                      db,
        ILogger<ReferralPerformanceService>       logger)
    {
        _db     = db;
        _logger = logger;
    }

    // BLK-SEC-02-01: tenantId scopes all Referral queries for TenantAdmin callers.
    // null = platform-wide (PlatformAdmin).
    public async Task<ReferralPerformanceResult> GetPerformanceAsync(
        DateTime          since,
        Guid?             tenantId  = null,
        CancellationToken ct        = default)
    {
        var nowUtc = DateTime.UtcNow;

        _logger.LogDebug(
            "LSCC-01-005 Computing referral performance from {From:O} to {Now:O} tenant={TenantId}.",
            since, nowUtc, tenantId?.ToString() ?? "platform");

        // BLK-SEC-02-01: Base query — apply tenant scope once.
        var referralsBase = _db.Referrals.AsQueryable();
        if (tenantId.HasValue)
            referralsBase = referralsBase.Where(r => r.TenantId == tenantId.Value);

        // ── 1. Referrals in window (with Provider eager-loaded for name) ─────────
        var windowReferrals = await referralsBase
            .AsNoTracking()
            .Include(r => r.Provider)
            .Where(r => r.CreatedAtUtc >= since)
            .Select(r => new
            {
                r.Id,
                r.CreatedAtUtc,
                r.Status,
                r.ProviderId,
                ProviderName = r.Provider != null ? r.Provider.Name : "Unknown",
            })
            .ToListAsync(ct);

        var referralIds = windowReferrals.Select(r => r.Id).ToHashSet();

        // ── 2. Earliest "Accepted" status history entry per referral ─────────────
        // Only load for referrals in our window to keep the query bounded.
        var acceptedHistoryMap = await _db.ReferralStatusHistories
            .AsNoTracking()
            .Where(h => referralIds.Contains(h.ReferralId) && h.NewStatus == "Accepted")
            .GroupBy(h => h.ReferralId)
            .Select(g => new
            {
                ReferralId      = g.Key,
                AcceptedAtUtc   = g.Min(h => h.ChangedAtUtc),  // earliest Accepted transition
            })
            .ToDictionaryAsync(x => x.ReferralId, x => x.AcceptedAtUtc, ct);

        // ── 3. Build RawReferralRecord list ──────────────────────────────────────
        var records = windowReferrals
            .Select(r => new RawReferralRecord(
                Id:           r.Id,
                CreatedAtUtc: r.CreatedAtUtc,
                Status:       r.Status,
                ProviderId:   r.ProviderId,
                ProviderName: r.ProviderName,
                AcceptedAtUtc: acceptedHistoryMap.TryGetValue(r.Id, out var at) ? at : null))
            .ToList();

        // ── 4. All currently-New referrals for aging distribution ────────────────
        // BLK-SEC-02-01: Use referralsBase (already tenant-scoped) for consistency.
        var currentNewReferrals = await referralsBase
            .AsNoTracking()
            .Where(r => r.Status == "New")
            .Select(r => new { r.Id, r.CreatedAtUtc })
            .ToListAsync(ct);

        var agingInput = currentNewReferrals
            .Select(r => (r.Id, r.CreatedAtUtc))
            .ToList();

        // ── 5. Compute and return ─────────────────────────────────────────────────
        return ReferralPerformanceCalculator.Compute(records, agingInput, nowUtc, since);
    }
}
