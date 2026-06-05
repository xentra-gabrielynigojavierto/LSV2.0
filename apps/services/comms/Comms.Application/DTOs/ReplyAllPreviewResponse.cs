namespace Comms.Application.DTOs;

public record ReplyAllPreviewResponse(
    Guid ConversationId,
    Guid? SourceEmailReferenceId,
    List<ReplyAllRecipient> ToRecipients,
    List<ReplyAllRecipient> CcRecipients,
    string? Subject);

public record ReplyAllRecipient(
    string NormalizedEmail,
    string? DisplayName);
