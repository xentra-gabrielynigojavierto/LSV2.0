namespace Support.Api.Domain;

public enum CommentType
{
    InternalNote,
    CustomerReply,
    SystemNote,
}

public enum CommentVisibility
{
    Internal,
    CustomerVisible,
}

public class SupportTicketComment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string TenantId { get; set; } = default!;
    public CommentType CommentType { get; set; }
    public CommentVisibility Visibility { get; set; }
    public string Body { get; set; } = default!;
    public string? AuthorUserId { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorEmail { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SupportTicketEvent
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string TenantId { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public string Summary { get; set; } = default!;
    public string? MetadataJson { get; set; }
    public string? ActorUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
