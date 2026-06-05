using BuildingBlocks.Authorization;
using Flow.Application.DTOs;
using Flow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.Api.Controllers.V1;

/// <summary>
/// LS-FLOW-E11.5 — read-only "My Tasks" surface for the calling
/// authenticated user. Backed by the new <see cref="Domain.Entities.WorkflowTask"/>
/// grain (E11.1) and tenant/user-scoped by construction.
///
/// <para>
/// <b>Identity:</b> the user id is resolved from the auth context via
/// <see cref="Domain.Interfaces.IFlowUserContext"/>; there is NO
/// <c>userId</c> path / query parameter, so user impersonation is
/// impossible by API shape, not just by policy.
/// </para>
///
/// <para>
/// <b>Tenant safety:</b> enforced by the global query filter on
/// <c>WorkflowTask</c>. A token from tenant <c>A</c> cannot return
/// rows from tenant <c>B</c> even if the row's <c>AssignedUserId</c>
/// happens to match — the filter is applied before the user-id predicate.
/// </para>
///
/// <para>
/// <b>Out of scope:</b> create / update / lifecycle mutations
/// (E11.2 / E11.4 own those), role / org-based task lists, admin task
/// queries, search, and free-text filtering.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/tasks")]
[Authorize(Policy = Policies.AuthenticatedUser)]
public sealed class MyTasksController : ControllerBase
{
    private readonly IMyTasksService _service;

    public MyTasksController(IMyTasksService service)
    {
        _service = service;
    }

    /// <summary>
    /// GET <c>/api/v1/tasks/me</c> — the calling user's assigned tasks.
    /// </summary>
    /// <param name="status">
    /// Optional. One or more <c>WorkflowTaskStatus</c> values
    /// (<c>Open</c>, <c>InProgress</c>, <c>Completed</c>, <c>Cancelled</c>).
    /// Repeat the parameter for multi-value: <c>?status=Open&amp;status=InProgress</c>.
    /// Comparison is case-insensitive; unknown values yield <c>400</c>.
    /// </param>
    /// <param name="page">1-based page index. Defaults to 1.</param>
    /// <param name="pageSize">Page size. Defaults to 25; max 100.</param>
    /// <returns>
    /// <see cref="PagedResponse{T}"/> of <see cref="MyTaskDto"/>.
    /// Always 200 with an empty list when the user has no matching
    /// tasks — never 404 — because the surface is a query, not a lookup.
    /// </returns>
    [HttpGet("me")]
    [ProducesResponseType(typeof(PagedResponse<MyTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTasks(
        [FromQuery(Name = "status")] string[]? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = MyTasksDefaults.DefaultPageSize,
        CancellationToken ct = default)
    {
        var query = new MyTasksQuery
        {
            Status   = status is { Length: > 0 } ? status : null,
            Page     = page,
            PageSize = pageSize,
        };

        var result = await _service.ListMyTasksAsync(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// LS-FLOW-E15 — GET <c>/api/v1/tasks/role-queue</c>. Returns
    /// open <c>RoleQueue</c> tasks the calling user is eligible to
    /// claim. Eligibility (<c>AssignedRole ∈ caller's roles</c>) is
    /// determined entirely server-side from <c>IFlowUserContext</c>;
    /// the request body has no role list so cross-role enumeration
    /// is impossible by API shape. Platform admins see every
    /// role-queue row in the tenant.
    /// </summary>
    [HttpGet("role-queue")]
    [ProducesResponseType(typeof(PagedResponse<MyTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoleQueue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = MyTasksDefaults.DefaultPageSize,
        CancellationToken ct = default)
    {
        var result = await _service.ListRoleQueueAsync(
            new RoleQueueQuery { Page = page, PageSize = pageSize }, ct);
        return Ok(result);
    }

    /// <summary>
    /// LS-FLOW-E15 — GET <c>/api/v1/tasks/org-queue</c>. Returns
    /// open <c>OrgQueue</c> tasks the calling user is eligible to
    /// claim (<c>AssignedOrgId == caller.OrgId</c>). Same security
    /// posture as <c>role-queue</c>: org id is server-derived only.
    /// Platform admins see every org-queue row in the tenant.
    /// </summary>
    [HttpGet("org-queue")]
    [ProducesResponseType(typeof(PagedResponse<MyTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrgQueue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = MyTasksDefaults.DefaultPageSize,
        CancellationToken ct = default)
    {
        var result = await _service.ListOrgQueueAsync(
            new OrgQueueQuery { Page = page, PageSize = pageSize }, ct);
        return Ok(result);
    }
}
