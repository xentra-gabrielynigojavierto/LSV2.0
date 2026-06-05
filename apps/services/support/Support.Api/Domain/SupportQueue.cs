namespace Support.Api.Domain;

public enum QueueMemberRole
{
    Agent,
    Lead,
    Manager,
}

public class SupportQueue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? ProductCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }
}

public class SupportQueueMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QueueId { get; set; }
    public string TenantId { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public QueueMemberRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
