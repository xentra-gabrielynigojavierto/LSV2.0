// LSCC-010: Auto-provisioning service interface.
using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

/// <summary>
/// Orchestrates the instant provider activation flow.
///
/// Happy path:
///   1. Validate HMAC token → load referral + provider
///   2. Check if provider is already active
///   3. Create/resolve Identity Organization via IIdentityOrganizationService
///   4. Link provider to org via IProviderService.LinkOrganizationAsync
///   5. Approve/upsert the LSCC-009 ActivationRequest
///   6. Emit tracking audit events
///   7. Return AutoProvisionResult.Provisioned(orgId, loginUrl)
///
/// Fallback path (any step fails):
///   - Upsert LSCC-009 ActivationRequest for admin review
///   - Return AutoProvisionResult.Fallback(reason)
/// </summary>
public interface IAutoProvisionService
{
    Task<AutoProvisionResult> ProvisionAsync(
        Guid              referralId,
        string            token,
        string?           requesterName,
        string?           requesterEmail,
        CancellationToken ct = default);
}
