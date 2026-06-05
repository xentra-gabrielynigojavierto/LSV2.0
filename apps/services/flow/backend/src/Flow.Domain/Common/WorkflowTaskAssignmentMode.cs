namespace Flow.Domain.Common;

/// <summary>
/// LS-FLOW-E14.1 — string-constant assignment-mode values for the
/// <see cref="Domain.Entities.WorkflowTask"/> work item.
///
/// <para>
/// Mirrors the existing <see cref="WorkflowTaskStatus"/> /
/// <see cref="WorkflowTaskPriority"/> convention: short, stable string
/// keys persisted as <c>varchar</c>. Keeping them out of an
/// <c>enum</c> avoids implicit ordinal coupling and lets future phases
/// extend the model (e.g. <c>TeamQueue</c>) without a schema
/// migration.
/// </para>
///
/// <para>
/// <b>Mode → required / forbidden fields.</b> See
/// <see cref="Domain.Entities.WorkflowTask.EnsureValid"/> for the
/// enforced invariants.
///   <list type="bullet">
///     <item><see cref="DirectUser"/> — <c>AssignedUserId</c> required;
///           <c>AssignedRole</c> and <c>AssignedOrgId</c> must be null.</item>
///     <item><see cref="RoleQueue"/>  — <c>AssignedRole</c> required;
///           <c>AssignedUserId</c> must be null.</item>
///     <item><see cref="OrgQueue"/>   — <c>AssignedOrgId</c> required;
///           <c>AssignedUserId</c> must be null.</item>
///     <item><see cref="Unassigned"/> — all three assignment fields must
///           be null. <c>AssignedAt</c> / <c>AssignedBy</c> must also be
///           null (no assignment event has occurred).</item>
///   </list>
/// </para>
/// </summary>
public static class WorkflowTaskAssignmentMode
{
    public const string DirectUser = "DirectUser";
    public const string RoleQueue  = "RoleQueue";
    public const string OrgQueue   = "OrgQueue";
    public const string Unassigned = "Unassigned";

    /// <summary>True when <paramref name="mode"/> is one of the four known values.</summary>
    public static bool IsKnown(string? mode) =>
        mode is DirectUser or RoleQueue or OrgQueue or Unassigned;

    /// <summary>
    /// Derive the assignment mode from the current values of the
    /// (user, role, org) fields. Used by
    /// <see cref="Domain.Entities.WorkflowTask.NormalizeAssignmentMode"/>
    /// as the lazy-mapping backstop for rows written by code paths that
    /// did not set the mode explicitly. Precedence is User &gt; Role &gt;
    /// Org &gt; Unassigned, mirroring the <see cref="WorkflowTaskAssignment"/>
    /// factory precedence in the E11.3 resolver.
    /// </summary>
    public static string Derive(string? assignedUserId, string? assignedRole, string? assignedOrgId)
    {
        if (!string.IsNullOrWhiteSpace(assignedUserId)) return DirectUser;
        if (!string.IsNullOrWhiteSpace(assignedRole))   return RoleQueue;
        if (!string.IsNullOrWhiteSpace(assignedOrgId))  return OrgQueue;
        return Unassigned;
    }
}
