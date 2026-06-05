using BuildingBlocks.Authorization;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;

namespace CareConnect.Application.Services;

/// <summary>
/// LSCC-01-002-02: Centralized, read-only provider access-readiness check.
///
/// The canonical provider-ready access bundle for CareConnect referral access is:
///   - CareConnectReceiver product role   (carrier of receiver capabilities)
///   - ReferralReadAddressed capability   (receiver-side referral read)
///   - ReferralAccept capability          (acceptance action)
///
/// Rules:
///   - no side effects: does not create users, assign roles, or link orgs
///   - deterministic: same inputs always produce the same result
///   - PlatformAdmin bypass is handled by the caller (AuthorizationService / endpoint policy);
///     this service evaluates raw product roles as given
/// </summary>
public sealed class ProviderAccessReadinessService : IProviderAccessReadinessService
{
    private readonly IPermissionService _perms;

    public ProviderAccessReadinessService(IPermissionService perms)
        => _perms = perms;

    public async Task<ProviderAccessReadinessResult> GetReadinessAsync(
        IReadOnlyCollection<string> productRoles,
        CancellationToken ct = default)
    {
        var hasReadAccess   = await _perms.HasPermissionAsync(productRoles, PermissionCodes.ReferralReadAddressed, ct);
        var hasAcceptAccess = await _perms.HasPermissionAsync(productRoles, PermissionCodes.ReferralAccept, ct);

        var hasReceiverRole = productRoles.Contains(
            ProductRoleCodes.CareConnectReceiver,
            StringComparer.OrdinalIgnoreCase);

        var isProvisioned = hasReadAccess && hasAcceptAccess;

        return new ProviderAccessReadinessResult
        {
            IsProvisioned     = isProvisioned,
            HasReceiverRole   = hasReceiverRole,
            HasReferralAccess = hasReadAccess,
            HasReferralAccept = hasAcceptAccess,
            Reason            = isProvisioned ? null : ReasonFor(hasReceiverRole, hasReadAccess, hasAcceptAccess),
        };
    }

    private static string ReasonFor(bool hasRole, bool hasRead, bool hasAccept) =>
        !hasRole   ? "missing-receiver-role" :
        !hasRead   ? "missing-referral-read-access" :
        !hasAccept ? "missing-referral-accept" :
                     "unknown";
}
