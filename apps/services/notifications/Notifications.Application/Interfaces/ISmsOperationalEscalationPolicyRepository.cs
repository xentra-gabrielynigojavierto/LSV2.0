using Notifications.Application.DTOs;
using Notifications.Domain;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-011: Repository for SMS alert escalation policy CRUD.
///
/// All responses mask the raw Target field.
/// Policies are disabled (soft-deleted) rather than physically removed.
/// </summary>
public interface ISmsOperationalEscalationPolicyRepository
{
    Task<SmsEscalationPolicyListResult> ListAsync(SmsEscalationPolicyQuery query, CancellationToken ct = default);

    Task<SmsOperationalEscalationPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all enabled policies that match the alert's type, severity,
    /// tenantId, provider, and providerConfigId (null fields are wildcards).
    /// </summary>
    Task<List<SmsOperationalEscalationPolicy>> GetEnabledMatchingPoliciesAsync(
        SmsOperationalAlert alert, CancellationToken ct = default);

    Task<SmsOperationalEscalationPolicy> CreateAsync(
        SmsOperationalEscalationPolicy policy, CancellationToken ct = default);

    Task UpdateAsync(SmsOperationalEscalationPolicy policy, CancellationToken ct = default);

    /// <summary>Sets Enabled=false without deleting the record.</summary>
    Task<bool> DisableAsync(Guid id, string? updatedBy, CancellationToken ct = default);
}
