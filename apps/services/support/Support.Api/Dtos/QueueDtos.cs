using Support.Api.Domain;

namespace Support.Api.Dtos;

public class CreateQueueRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? ProductCode { get; set; }
    public bool? IsActive { get; set; }
}

public class UpdateQueueRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ProductCode { get; set; }
    public bool? IsActive { get; set; }
}

public class QueueResponse
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? ProductCode { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }

    public static QueueResponse From(SupportQueue q) => new()
    {
        Id = q.Id,
        TenantId = q.TenantId,
        Name = q.Name,
        Description = q.Description,
        ProductCode = q.ProductCode,
        IsActive = q.IsActive,
        CreatedAt = q.CreatedAt,
        UpdatedAt = q.UpdatedAt,
        CreatedByUserId = q.CreatedByUserId,
        UpdatedByUserId = q.UpdatedByUserId,
    };
}

public class AddQueueMemberRequest
{
    public string UserId { get; set; } = default!;
    public QueueMemberRole Role { get; set; }
    public bool? IsActive { get; set; }
}

public class QueueMemberResponse
{
    public Guid Id { get; set; }
    public Guid QueueId { get; set; }
    public string TenantId { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public QueueMemberRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static QueueMemberResponse From(SupportQueueMember m) => new()
    {
        Id = m.Id,
        QueueId = m.QueueId,
        TenantId = m.TenantId,
        UserId = m.UserId,
        Role = m.Role,
        IsActive = m.IsActive,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
    };
}

public class AssignTicketRequest
{
    public string? AssignedUserId { get; set; }
    public Guid? AssignedQueueId { get; set; }
    public bool? ClearAssignment { get; set; }
}
