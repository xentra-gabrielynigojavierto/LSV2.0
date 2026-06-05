namespace Comms.Application.DTOs;

public record OperationalQueryRequest(
    Guid? QueueId = null,
    Guid? AssignedUserId = null,
    string? AssignmentStatus = null,
    string? Priority = null,
    string? OperationalStatus = null,
    string? WaitingState = null,
    bool? BreachedFirstResponse = null,
    bool? BreachedResolution = null,
    bool? HasWarnings = null,
    Guid? MentionedUserId = null,
    bool? UnreadOnly = null,
    DateTime? UpdatedSince = null,
    DateTime? CreatedSince = null,
    int Page = 1,
    int PageSize = 50,
    string SortBy = "lastActivityAtUtc",
    string SortDirection = "desc");

public record ConversationOperationalListItemResponse(
    Guid ConversationId,
    string Subject,
    string OperationalStatus,
    Guid? QueueId,
    string? QueueName,
    Guid? AssignedUserId,
    string? AssignmentStatus,
    string? Priority,
    string? WaitingState,
    bool BreachedFirstResponse,
    bool BreachedResolution,
    DateTime? FirstResponseDueAtUtc,
    DateTime? ResolutionDueAtUtc,
    DateTime LastActivityAtUtc,
    DateTime CreatedAtUtc,
    bool IsUnread,
    int MentionCount,
    string? LastMessageSnippet);

public record OperationalQueryResponse(
    List<ConversationOperationalListItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasMore);
