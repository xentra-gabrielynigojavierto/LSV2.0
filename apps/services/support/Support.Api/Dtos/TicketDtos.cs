using Support.Api.Domain;

namespace Support.Api.Dtos;

public class CreateTicketRequest
{
    public string? TenantId { get; set; }
    public string? ProductCode { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public TicketSeverity? Severity { get; set; }
    public string? Category { get; set; }
    public TicketSource Source { get; set; } = TicketSource.Portal;
    public string? RequesterUserId { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }

    /// <summary>
    /// Optional — when provided, the ticket is linked to an ExternalCustomer
    /// resolved or created by this email within the tenant.
    /// Sets RequesterType=ExternalCustomer and VisibilityScope=CustomerVisible.
    /// </summary>
    public string? ExternalCustomerEmail { get; set; }

    /// <summary>
    /// Optional display name for the external customer. Ignored if ExternalCustomerEmail is null.
    /// Only used during ExternalCustomer creation; ignored if the customer already exists.
    /// </summary>
    public string? ExternalCustomerName { get; set; }
}

public class UpdateTicketRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public TicketStatus? Status { get; set; }
    public TicketPriority? Priority { get; set; }
    public TicketSeverity? Severity { get; set; }
    public string? Category { get; set; }
    public string? AssignedUserId { get; set; }
    public string? AssignedQueueId { get; set; }
    public DateTime? DueAt { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }
}

public class TicketResponse
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = default!;
    public string? ProductCode { get; set; }
    public string TicketNumber { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketSeverity? Severity { get; set; }
    public string? Category { get; set; }
    public TicketSource Source { get; set; }
    public string? RequesterUserId { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }
    public TicketRequesterType RequesterType { get; set; }
    public Guid? ExternalCustomerId { get; set; }
    public TicketVisibilityScope VisibilityScope { get; set; }
    public string? AssignedUserId { get; set; }
    public string? AssignedQueueId { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }

    public static TicketResponse From(SupportTicket t) => new()
    {
        Id = t.Id,
        TenantId = t.TenantId,
        ProductCode = t.ProductCode,
        TicketNumber = t.TicketNumber,
        Title = t.Title,
        Description = t.Description,
        Status = t.Status,
        Priority = t.Priority,
        Severity = t.Severity,
        Category = t.Category,
        Source = t.Source,
        RequesterUserId = t.RequesterUserId,
        RequesterName = t.RequesterName,
        RequesterEmail = t.RequesterEmail,
        RequesterType = t.RequesterType,
        ExternalCustomerId = t.ExternalCustomerId,
        VisibilityScope = t.VisibilityScope,
        AssignedUserId = t.AssignedUserId,
        AssignedQueueId = t.AssignedQueueId,
        DueAt = t.DueAt,
        ResolvedAt = t.ResolvedAt,
        ClosedAt = t.ClosedAt,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        CreatedByUserId = t.CreatedByUserId,
        UpdatedByUserId = t.UpdatedByUserId,
    };
}

public class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long Total { get; set; }
}
