namespace Support.Api.Domain;

public class SupportTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = default!;
    public string? ProductCode { get; set; }
    public string TicketNumber { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public TicketSeverity? Severity { get; set; }
    public string? Category { get; set; }
    public TicketSource Source { get; set; } = TicketSource.Portal;
    public string? RequesterUserId { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }
    public TicketRequesterType RequesterType { get; set; } = TicketRequesterType.InternalUser;
    public Guid? ExternalCustomerId { get; set; }
    public TicketVisibilityScope VisibilityScope { get; set; } = TicketVisibilityScope.Internal;
    public string? AssignedUserId { get; set; }
    public string? AssignedQueueId { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }
}

public class TicketNumberSequence
{
    public string TenantId { get; set; } = default!;
    public int Year { get; set; }
    public long LastValue { get; set; }
    public uint RowVersion { get; set; }
}
