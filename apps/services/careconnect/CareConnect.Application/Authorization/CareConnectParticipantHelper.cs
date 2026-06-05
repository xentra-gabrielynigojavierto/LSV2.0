// LSCC-002: Shared participant-scoping logic for CareConnect records.
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Domain;

namespace CareConnect.Application.Authorization;

/// <summary>
/// Centralised participant-access helpers for CareConnect.
/// Used consistently for list scoping and row-level access across Referral and Appointment.
///
/// Golden rule: when org context is missing or the caller is not a participant,
/// access is DENIED — never widened silently.
/// </summary>
// LSCC-002: Shared participant-scoping logic — single definition for all CareConnect records
public static class CareConnectParticipantHelper
{
    // ── Admin bypass ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the caller is a PlatformAdmin or TenantAdmin.
    /// These roles bypass all org-participant checks per platform conventions.
    /// </summary>
    public static bool IsAdmin(ICurrentRequestContext ctx) =>
        ctx.IsPlatformAdmin || ctx.Roles.Contains(Roles.TenantAdmin, StringComparer.OrdinalIgnoreCase);

    // ── Referral participant checks ───────────────────────────────────────────

    /// <summary>
    /// Returns true when the caller's org is a participant in the given referral
    /// (i.e. they are the referring or receiving org).
    /// </summary>
    /// <param name="referral">The loaded referral entity.</param>
    /// <param name="callerOrgId">
    /// The org ID from the caller's JWT context. A null value always returns false
    /// (missing context must never widen access).
    /// </param>
    // LSCC-002: CareConnect permission enforcement — referral participant check
    public static bool IsReferralParticipant(Referral referral, Guid? callerOrgId)
    {
        if (callerOrgId is null) return false;

        return referral.ReferringOrganizationId == callerOrgId
            || referral.ReceivingOrganizationId == callerOrgId;
    }

    /// <summary>
    /// Determines the referral org filter fields for a list query based on caller context.
    /// Returns (referringOrgId: null, receivingOrgId: null) for admins — no filter applied.
    /// </summary>
    public static (Guid? ReferringOrgId, Guid? ReceivingOrgId) GetReferralOrgScope(
        ICurrentRequestContext ctx,
        bool callerIsReceiver)
    {
        if (IsAdmin(ctx)) return (null, null);

        if (callerIsReceiver)
            return (null, ctx.OrgId);

        return (ctx.OrgId, null);
    }

    // ── Appointment participant checks ────────────────────────────────────────

    /// <summary>
    /// Returns true when the caller's org is a participant in the given appointment
    /// (i.e. they are the referring or receiving org denormalized from the source referral).
    /// </summary>
    /// <param name="appointment">The loaded appointment entity.</param>
    /// <param name="callerOrgId">
    /// The org ID from the caller's JWT context. A null value always returns false.
    /// </param>
    // LSCC-002: CareConnect permission enforcement — appointment participant check
    public static bool IsAppointmentParticipant(Appointment appointment, Guid? callerOrgId)
    {
        if (callerOrgId is null) return false;

        return appointment.ReferringOrganizationId == callerOrgId
            || appointment.ReceivingOrganizationId == callerOrgId;
    }

    /// <summary>
    /// Determines the appointment org filter fields for a list query based on caller context.
    /// Follows the same convention as referral scoping:
    /// receivers filter by receiving org; all others filter by referring org.
    /// Returns (null, null) for admins.
    /// </summary>
    public static (Guid? ReferringOrgId, Guid? ReceivingOrgId) GetAppointmentOrgScope(
        ICurrentRequestContext ctx,
        bool callerIsReceiver)
    {
        if (IsAdmin(ctx)) return (null, null);

        if (callerIsReceiver)
            return (null, ctx.OrgId);

        return (ctx.OrgId, null);
    }
}
