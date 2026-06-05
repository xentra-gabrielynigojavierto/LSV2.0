using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using Microsoft.Extensions.Logging;

namespace CareConnect.Application.Services;

/// <summary>
/// LSCC-01-004: Best-effort blocked-access event logger.
///
/// Writes a BlockedProviderAccessLog row whenever a provider fails the access-readiness check.
/// All failures are swallowed with a warning log — this service must never block the user flow.
/// </summary>
public sealed class BlockedAccessLogService : IBlockedAccessLogService
{
    private readonly IBlockedAccessLogRepository       _repo;
    private readonly ILogger<BlockedAccessLogService>  _logger;

    public BlockedAccessLogService(
        IBlockedAccessLogRepository repo,
        ILogger<BlockedAccessLogService> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task LogAsync(
        Guid?   tenantId,
        Guid?   userId,
        string? userEmail,
        Guid?   organizationId,
        Guid?   providerId,
        Guid?   referralId,
        string  failureReason,
        CancellationToken ct = default)
    {
        try
        {
            var entry = BlockedProviderAccessLog.Create(
                tenantId:       tenantId,
                userId:         userId,
                userEmail:      userEmail,
                organizationId: organizationId,
                providerId:     providerId,
                referralId:     referralId,
                failureReason:  failureReason);

            await _repo.AddAsync(entry, ct);
        }
        catch (Exception ex)
        {
            // Best-effort: log and swallow — never allow logging to fail the user request.
            _logger.LogWarning(ex,
                "LSCC-01-004: Failed to write blocked-access log entry for userId={UserId} reason={Reason}",
                userId, failureReason);
        }
    }
}
