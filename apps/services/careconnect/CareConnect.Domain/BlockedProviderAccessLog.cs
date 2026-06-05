namespace CareConnect.Domain;

/// <summary>
/// LSCC-01-004: Operational visibility — records every failed provider access-readiness
/// check so admins can identify who is blocked, why, and when.
///
/// Written best-effort: a log failure must never block the user-facing flow.
/// </summary>
public class BlockedProviderAccessLog
{
    public Guid      Id              { get; private set; }
    public Guid?     TenantId        { get; private set; }
    public Guid?     UserId          { get; private set; }
    public string?   UserEmail       { get; private set; }
    public Guid?     OrganizationId  { get; private set; }
    public Guid?     ProviderId      { get; private set; }
    public Guid?     ReferralId      { get; private set; }
    public string    FailureReason   { get; private set; } = string.Empty;
    public DateTime  AttemptedAtUtc  { get; private set; }

    private BlockedProviderAccessLog() { }

    public static BlockedProviderAccessLog Create(
        Guid?   tenantId,
        Guid?   userId,
        string? userEmail,
        Guid?   organizationId,
        Guid?   providerId,
        Guid?   referralId,
        string  failureReason)
    {
        return new BlockedProviderAccessLog
        {
            Id             = Guid.NewGuid(),
            TenantId       = tenantId,
            UserId         = userId,
            UserEmail      = userEmail?.ToLowerInvariant().Trim(),
            OrganizationId = organizationId,
            ProviderId     = providerId,
            ReferralId     = referralId,
            FailureReason  = failureReason,
            AttemptedAtUtc = DateTime.UtcNow,
        };
    }
}
