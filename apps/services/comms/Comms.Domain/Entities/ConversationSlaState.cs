using BuildingBlocks.Domain;
using Comms.Domain.Constants;
using Comms.Domain.Enums;

namespace Comms.Domain.Entities;

public class ConversationSlaState : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public string Priority { get; private set; } = ConversationPriority.Normal;
    public DateTime? FirstResponseDueAtUtc { get; private set; }
    public DateTime? ResolutionDueAtUtc { get; private set; }
    public DateTime? FirstResponseAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public bool BreachedFirstResponse { get; private set; }
    public bool BreachedResolution { get; private set; }
    public string WaitingOn { get; private set; } = Enums.WaitingState.None;
    public DateTime? LastEvaluatedAtUtc { get; private set; }
    public DateTime SlaStartedAtUtc { get; private set; }

    private ConversationSlaState() { }

    public static ConversationSlaState Initialize(
        Guid tenantId,
        Guid conversationId,
        string priority,
        DateTime startAtUtc,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (conversationId == Guid.Empty) throw new ArgumentException("ConversationId is required.", nameof(conversationId));
        if (!ConversationPriority.All.Contains(priority))
            throw new ArgumentException($"Invalid priority: '{priority}'.");

        var durations = SlaDefaults.GetDurations(priority);
        var now = DateTime.UtcNow;

        return new ConversationSlaState
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            Priority = priority,
            FirstResponseDueAtUtc = startAtUtc.Add(durations.FirstResponse),
            ResolutionDueAtUtc = startAtUtc.Add(durations.Resolution),
            BreachedFirstResponse = false,
            BreachedResolution = false,
            WaitingOn = Enums.WaitingState.WaitingInternal,
            SlaStartedAtUtc = startAtUtc,
            LastEvaluatedAtUtc = now,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void SatisfyFirstResponse(DateTime respondedAtUtc, Guid updatedByUserId)
    {
        if (FirstResponseAtUtc.HasValue) return;

        FirstResponseAtUtc = respondedAtUtc;
        WaitingOn = Enums.WaitingState.WaitingExternal;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SatisfyResolution(DateTime resolvedAtUtc, Guid updatedByUserId)
    {
        if (ResolvedAtUtc.HasValue) return;

        ResolvedAtUtc = resolvedAtUtc;
        WaitingOn = Enums.WaitingState.None;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void EvaluateBreaches(DateTime nowUtc)
    {
        if (!BreachedFirstResponse && FirstResponseAtUtc is null && FirstResponseDueAtUtc.HasValue && nowUtc > FirstResponseDueAtUtc.Value)
            BreachedFirstResponse = true;

        if (!BreachedResolution && ResolvedAtUtc is null && ResolutionDueAtUtc.HasValue && nowUtc > ResolutionDueAtUtc.Value)
            BreachedResolution = true;

        LastEvaluatedAtUtc = nowUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdatePriority(string newPriority, DateTime nowUtc, Guid updatedByUserId)
    {
        if (!ConversationPriority.All.Contains(newPriority))
            throw new ArgumentException($"Invalid priority: '{newPriority}'.");

        var oldPriority = Priority;
        Priority = newPriority;

        var durations = SlaDefaults.GetDurations(newPriority);

        if (FirstResponseAtUtc is null)
            FirstResponseDueAtUtc = SlaStartedAtUtc.Add(durations.FirstResponse);

        if (ResolvedAtUtc is null)
            ResolutionDueAtUtc = SlaStartedAtUtc.Add(durations.Resolution);

        EvaluateBreaches(nowUtc);

        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetWaitingOn(string waitingState, Guid updatedByUserId)
    {
        if (!Enums.WaitingState.All.Contains(waitingState))
            throw new ArgumentException($"Invalid waiting state: '{waitingState}'.");

        WaitingOn = waitingState;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
