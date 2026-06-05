using Support.Api.Domain;

namespace Support.Api.Dtos;

public class CreateCommentRequest
{
    public string Body { get; set; } = default!;
    public CommentType? CommentType { get; set; }
    public CommentVisibility? Visibility { get; set; }
    public string? AuthorUserId { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorEmail { get; set; }
}

public class CommentResponse
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public CommentType CommentType { get; set; }
    public CommentVisibility Visibility { get; set; }
    public string Body { get; set; } = default!;
    public string? AuthorUserId { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorEmail { get; set; }
    public DateTime CreatedAt { get; set; }

    public static CommentResponse From(SupportTicketComment c) => new()
    {
        Id = c.Id,
        TicketId = c.TicketId,
        CommentType = c.CommentType,
        Visibility = c.Visibility,
        Body = c.Body,
        AuthorUserId = c.AuthorUserId,
        AuthorName = c.AuthorName,
        AuthorEmail = c.AuthorEmail,
        CreatedAt = c.CreatedAt,
    };
}

public class TimelineItem
{
    public string Type { get; set; } = default!; // "comment" | "event"
    public DateTime CreatedAt { get; set; }
    public string? Summary { get; set; }
    public string? Body { get; set; }
    public string? EventType { get; set; }
    public string? CommentType { get; set; }
    public string? Visibility { get; set; }
    public string? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string? ActorEmail { get; set; }
    public string? MetadataJson { get; set; }
}
