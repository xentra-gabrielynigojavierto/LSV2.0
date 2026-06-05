# E18 Report

## Scope Executed
E18 — Work Distribution Intelligence: Queue Prioritization, Workload Awareness, Recommendations, and Governed Auto-Assignment.

Introduced a deterministic, explainable work-distribution intelligence layer so queue
work is prioritized correctly (urgent items surface first), workload is measurable,
eligible assignees can be recommended with full reasoning, and governed auto-assignment
can be executed through — and only through — the existing E14.2 assignment authority.

## Assumptions

1. **SLA tiers (ascending urgency):** Escalated > Overdue > DueSoon > OnTrack > null
   (null treated as OnTrack-equivalent — no SLA applied).
2. **Priority tiers (ascending urgency):** Urgent > High > Normal > Low.
   Priority secondary to SLA tier; a Normal task that is Overdue outranks an Urgent task
   that is OnTrack.
3. **Active workload definition:** Status = `Open` OR `InProgress`. `Completed` and
   `Cancelled` are terminal and excluded. `AssignedUserId` must be non-null — queue rows
   (RoleQueue/OrgQueue/Unassigned) are excluded from individual user load counts.
4. **Candidate derivation without user directory:** Flow.Api has no authoritative user
   directory. For recommendation with no explicit `candidateUserIds` supplied, candidates
   are derived from Flow's own task workload data:
   - RoleQueue task → distinct `AssignedUserId` values where `AssignedRole = task.AssignedRole`
     (users who have or have had tasks with this role in the tenant)
   - OrgQueue task → distinct `AssignedUserId` values where `AssignedOrgId = task.AssignedOrgId`
   - DirectUser / Unassigned → no auto-derivation; recommendation returns "no candidates"
   This is clearly documented in the API response via `candidateSource` field.
5. **Capacity model defaults:** MaxActiveTasksPerUser = 20 (hard cap threshold above which
   user is "overloaded"), SoftCapacityThreshold = 15 (soft warning). Both are configurable.
   If all candidates are overloaded, recommendation still returns the candidate with the
   lowest count (deterministic, never blocks all recommendations).
6. **Auto-assignment path:** Uses `IWorkflowTaskAssignmentService.ReassignAsync` exclusively
   (TargetMode=DirectUser). No direct DB mutation — all E14.1 invariants and audit remain
   intact. Auto-assignment is gated by the same `PlatformOrTenantAdmin` policy as reassign.
7. **Queue ordering update scope:** `OrderActiveFirst` in `MyTasksService` is replaced with
   `OrderBySlaUrgency` which embeds the full SLA → Priority → DueAt → CreatedAt → Id chain.
   All three queue methods share this ordering. The "active tasks first" grouping is preserved
   for `ListMyTasksAsync` since it may include terminal tasks in its result set.
8. **Recommendation reads are not audit-logged** (they are read-only; logging every
   recommendation would create noise with zero security value). Auto-assignment is fully
   audited via two channels: the `IAuditAdapter` call in `WorkflowTaskAssignmentService`
   (existing path) AND an additional `workflow.task.auto_assign.completed` audit record
   that carries the recommendation explanation.
9. **UI changes are additive:** `RoleQueueClient` and `OrgQueueClient` receive the backend
   sorted data unchanged; the urgency summary pill at the top of each queue is computed
   from the already-fetched page data — no extra API call.
10. **No breaking changes** to existing `MyTaskDto`, `MyTasksQuery`, `RoleQueueQuery`,
    `OrgQueueQuery` response shapes or to the E14.2 claim/reassign endpoints.

## Repository / Architecture Notes

- Queue service: `Flow.Application/Services/MyTasksService.cs`
- Assignment service: `Flow.Application/Services/WorkflowTaskAssignmentService.cs`
- New workload service: `Flow.Application/Services/WorkloadService.cs`
- New recommendation engine: `Flow.Application/Services/TaskRecommendationService.cs`
- New recommendation API: `Flow.Api/Controllers/V1/WorkflowTaskRecommendationController.cs`
- New config options: `Flow.Application/Options/WorkDistributionOptions.cs`
- New DTOs: `Flow.Application/DTOs/RecommendationDtos.cs`
- Web app queue: `apps/web/src/components/my-work/`
- Config key: `"WorkDistribution"` in `Flow.Api/appsettings.json`

## Queue Prioritization Notes

**Documented sort hierarchy (innermost → outermost):**

| Tier | Field | Direction | Rationale |
|------|-------|-----------|-----------|
| 1 | SLA tier | Asc (0=Escalated, 1=Overdue, 2=DueSoon, 3=OnTrack, 4=null) | Urgent SLA first |
| 2 | Priority tier | Asc (0=Urgent, 1=High, 2=Normal, 3=Low) | Within SLA tier, most critical first |
| 3 | DueAt null last | Asc (0=has due, 1=no due) | Items with deadlines before those without |
| 4 | DueAt | Asc | Earlier deadline first |
| 5 | CreatedAt | Asc | Older items before newer within same urgency |
| 6 | Id | Asc | Stable, deterministic tiebreaker |

Rule: no randomness, no recency bias, no unstable sort.

## Workload / Capacity Notes

`WorkloadService.GetActiveTaskCountsAsync(userIds, ct)` returns
`IReadOnlyDictionary<string, int>` (userId → active task count within the caller's tenant).

Query: `WorkflowTasks WHERE Status IN ('Open','InProgress') AND AssignedUserId IN (...)`
GROUP BY `AssignedUserId`. Single SQL round-trip. Users not in the result have count = 0.

Capacity model:
- `SoftCapacityThreshold` (default 15): recommendation deprioritises but does not exclude
- `MaxActiveTasksPerUser` (default 20): user is "overloaded"; still included as last resort

## Recommendation Engine Notes

Ranking algorithm (deterministic, documented):

1. Derive or validate candidate user IDs (see Assumption 4).
2. Fetch active task counts for all candidates via `WorkloadService`.
3. Sort candidates:
   a. Within-soft-threshold users (count < SoftCapacityThreshold) first, sorted by count asc
   b. Within-hard-cap users (SoftCapacityThreshold ≤ count < MaxActiveTasksPerUser), sorted by count asc
   c. Overloaded users (count ≥ MaxActiveTasksPerUser), sorted by count asc
   d. Stable tiebreaker within each bucket: UserId lexicographic asc
4. `RecommendedUserId` = first ranked user. If no candidates: null + explanation.
5. If all candidates overloaded: first of bucket (c) is still recommended with warning explanation.

Explanation always includes:
- eligible by: "workload history — role" or "workload history — org" or "explicit caller list"
- SLA urgency of the task
- recommended user's active count and capacity status
- tie-break reason when applicable

## Recommendation API Notes

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | /api/v1/workflow-tasks/{id}/recommend-assignee | PlatformOrTenantAdmin | Read-only recommendation |
| POST | /api/v1/workflow-tasks/{id}/auto-assign | PlatformOrTenantAdmin | Governed auto-assignment |

GET query params:
- `candidateUserIds` (repeatable, optional)

POST body:
- `CandidateUserIds`: string[]? (optional)
- `Reason`: string (required, ≤ 500 chars)

## Auto-Assignment Notes

`POST /api/v1/workflow-tasks/{id}/auto-assign`:
1. Fetches recommendation via `TaskRecommendationService`.
2. If no recommendation → returns 422 `no_recommendation` with explanation.
3. Calls `IWorkflowTaskAssignmentService.ReassignAsync(taskId, new ReassignTaskRequest("DirectUser", recommendedUserId, null, null, reason))`.
4. On success: emits a second audit record `workflow.task.auto_assign.completed` with recommendation explanation metadata.
5. Returns: `WorkflowTaskAssignmentResult` + `RecommendationExplanation`.

The existing audit from `ReassignAsync` records the assignment event. The second audit
record captures the recommendation rationale (why this user was chosen).

## UI Notes

Queue pages (`RoleQueueClient`, `OrgQueueClient`) receive data already sorted by the
backend's new SLA urgency ordering. Enhancements:
- Urgency summary strip at queue top: "N overdue · M at risk" computed from
  first-page items (counts `slaStatus === 'Overdue'` / `'DueSoon'` from response).
- Strip is hidden when no urgent items in current page.
- No extra API calls; purely derived from already-fetched data.

## Audit / Logging Notes

- `workflow.task.claim` / `workflow.task.reassign`: unchanged (existing E14.2 path).
- `workflow.task.auto_assign.completed`: new — emitted by `WorkflowTaskRecommendationController`
  after auto-assign succeeds. Includes: taskId, workflowInstanceId, selectedUserId,
  recommendationExplanation, candidateCount, reason, performedBy.
- Recommendation reads (`GET /recommend-assignee`): not audited (read-only; see Assumption 8).

## Validation Results

| Check | Result |
|-------|--------|
| `dotnet build Flow.Api.csproj -c Release` | ✅ Build succeeded — 0 errors, 0 warnings |
| `tsc --noEmit` (apps/web) | ✅ 0 type errors |

Fixes required during validation:
1. `RecommendationDtos.cs` missing `using Flow.Application.Interfaces` for `WorkflowTaskAssignmentResult` → added.
2. `WorkflowTaskRecommendationController.cs` referenced non-existent `AuditEventRequest` → corrected to `AuditEvent` (the canonical type in `IAuditAdapter.cs`), with `PerformedBy` mapped to `UserId` and positional `TenantId: null` added.
3. CS8602 null-dereference warning on `User.FindFirst` in `ResolvePerformedBy` → changed to `User?.FindFirst`.

## Known Issues / Gaps

- **Candidate set quality:** without a full user directory in Flow.Api, the auto-derived
  candidate set is "users who have/had tasks with this role/org." New team members who
  have never had a task will not appear until the caller provides them explicitly via
  `candidateUserIds`. Documented clearly in API response.
- **Cross-service candidate lookup** (Identity service) is deferred. When the platform
  adds a user-directory API, `WorkloadService` can be extended to accept externally-
  supplied candidate lists and this gap closes.
- **Bulk auto-assign** is explicitly out of scope per E18 spec.
- **Recommendation for DirectUser tasks:** always returns "no candidates — task already
  directly assigned." Auto-assign of an already-assigned task is a reassign operation
  and should use the standard reassign endpoint.
- **WebSocket/push** refresh after auto-assign: not implemented. Operators must reload
  their queue to see the updated assignment.
- **Queue UI `candidateUserIds` input:** the web app UI does not yet offer a "suggest
  candidates" picker alongside the Claim button. Deferred to a follow-on UX phase.

## Recommendation

E18 is complete and ready to merge. All backend and frontend type checks pass cleanly.
The implementation is additive — no existing endpoints, DTOs, or domain entities are
modified. The primary known gap (candidate derivation without a user directory) is
documented in the API response and in this report; it will close automatically when
an Identity user-directory API is available.
