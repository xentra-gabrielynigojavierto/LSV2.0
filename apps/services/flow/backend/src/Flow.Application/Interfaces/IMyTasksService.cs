using Flow.Application.DTOs;

namespace Flow.Application.Interfaces;

/// <summary>
/// LS-FLOW-E11.5 — read-only query surface for the calling user's
/// assigned <see cref="Domain.Entities.WorkflowTask"/> rows, widened
/// in **LS-FLOW-E15** to also serve the Role Queue, Org Queue, and
/// single-task detail surfaces.
///
/// <para>
/// Tenant scoping is enforced automatically by the
/// <see cref="Infrastructure.Persistence.FlowDbContext"/> global query
/// filter on <c>WorkflowTask</c>. Each method below adds further
/// caller-scoped predicates (user id / roles / org id / id) so
/// cross-tenant and cross-eligibility access are impossible by
/// construction.
/// </para>
/// </summary>
public interface IMyTasksService
{
    /// <summary>
    /// LS-FLOW-E11.5 — the calling user's <c>DirectUser</c> tasks.
    /// Ordering: active tasks (<c>Open</c>, <c>InProgress</c>) first,
    /// then <c>UpdatedAt DESC</c> (falling back to <c>CreatedAt</c>),
    /// with <c>Id</c> as a stable tiebreaker.
    /// </summary>
    Task<PagedResponse<MyTaskDto>> ListMyTasksAsync(MyTasksQuery query, CancellationToken ct = default);

    /// <summary>
    /// LS-FLOW-E15 — open <c>RoleQueue</c> tasks the caller is
    /// eligible to claim. Filter:
    /// <c>AssignmentMode = RoleQueue AND Status = Open AND
    /// AssignedRole ∈ caller's Roles</c>. Platform admins see every
    /// role-queue row in the tenant.
    /// </summary>
    Task<PagedResponse<MyTaskDto>> ListRoleQueueAsync(RoleQueueQuery query, CancellationToken ct = default);

    /// <summary>
    /// LS-FLOW-E15 — open <c>OrgQueue</c> tasks the caller is
    /// eligible to claim. Filter:
    /// <c>AssignmentMode = OrgQueue AND Status = Open AND
    /// AssignedOrgId = caller's OrgId</c>. Platform admins see every
    /// org-queue row in the tenant.
    /// </summary>
    Task<PagedResponse<MyTaskDto>> ListOrgQueueAsync(OrgQueueQuery query, CancellationToken ct = default);

    /// <summary>
    /// LS-FLOW-E15 — a single task by id, returned with the same
    /// widened DTO used by the list surfaces. Tenant filter applies
    /// (cross-tenant / missing id ⇒ <c>NotFoundException</c> ⇒ 404).
    /// No additional eligibility check: the operator portal needs to
    /// be able to inspect any tenant-visible task it can navigate to.
    /// Mutation authorisation is a separate concern owned by the
    /// assignment / lifecycle services.
    /// </summary>
    Task<MyTaskDto> GetTaskDetailAsync(Guid taskId, CancellationToken ct = default);
}
