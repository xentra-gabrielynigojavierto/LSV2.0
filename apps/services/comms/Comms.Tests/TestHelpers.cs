using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Infrastructure.Persistence;
using Comms.Infrastructure.Repositories;
using Comms.Application.Services;
using Comms.Domain.Entities;
using Comms.Domain.Enums;

namespace Comms.Tests;

public static class TestHelpers
{
    public static readonly Guid TenantId = Guid.NewGuid();
    public static readonly Guid OrgId = Guid.NewGuid();
    public static readonly Guid UserId1 = Guid.NewGuid();
    public static readonly Guid UserId2 = Guid.NewGuid();
    public static readonly Guid ExternalUserId = Guid.NewGuid();

    public static CommsDbContext CreateDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<CommsDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new CommsDbContext(options);
    }

    public static Conversation CreateTestConversation(
        string status = "New",
        string visibility = "InternalOnly")
    {
        return Conversation.Create(
            TenantId, OrgId, "SYNQ_COMMS",
            ContextType.Case, "case-123",
            "Test Conversation", visibility, UserId1);
    }

    public static ConversationParticipant CreateTestParticipant(
        Guid conversationId,
        Guid? userId = null,
        string participantType = "InternalUser",
        bool canReply = true,
        string? externalName = null,
        string? externalEmail = null)
    {
        return ConversationParticipant.Create(
            conversationId, TenantId, OrgId,
            participantType, ParticipantRole.Participant, canReply,
            UserId1,
            userId: userId ?? UserId1,
            externalName: externalName,
            externalEmail: externalEmail);
    }

    public static Message CreateTestMessage(
        Guid conversationId,
        string visibility = "InternalOnly",
        string channel = "InApp",
        Guid? senderUserId = null)
    {
        return Message.Create(
            conversationId, TenantId, OrgId,
            channel, Direction.Internal,
            $"Test message {Guid.NewGuid()}", visibility,
            senderUserId ?? UserId1,
            senderUserId: senderUserId ?? UserId1,
            senderParticipantType: ParticipantType.InternalUser);
    }

    public static IConversationRepository CreateConversationRepo(CommsDbContext db) =>
        new ConversationRepository(db);

    public static IMessageRepository CreateMessageRepo(CommsDbContext db) =>
        new MessageRepository(db);

    public static IParticipantRepository CreateParticipantRepo(CommsDbContext db) =>
        new ParticipantRepository(db);

    public static IConversationReadStateRepository CreateReadStateRepo(CommsDbContext db) =>
        new ConversationReadStateRepository(db);

    public static IMessageAttachmentRepository CreateAttachmentRepo(CommsDbContext db) =>
        new MessageAttachmentRepository(db);

    public static IEmailMessageReferenceRepository CreateEmailRefRepo(CommsDbContext db) =>
        new EmailMessageReferenceRepository(db);

    public static IExternalParticipantIdentityRepository CreateIdentityRepo(CommsDbContext db) =>
        new ExternalParticipantIdentityRepository(db);

    public static IEmailDeliveryStateRepository CreateDeliveryStateRepo(CommsDbContext db) =>
        new EmailDeliveryStateRepository(db);

    public static IEmailRecipientRecordRepository CreateRecipientRepo(CommsDbContext db) =>
        new EmailRecipientRecordRepository(db);

    public static ITenantEmailSenderConfigRepository CreateSenderConfigRepo(CommsDbContext db) =>
        new TenantEmailSenderConfigRepository(db);

    public static IEmailTemplateConfigRepository CreateTemplateConfigRepo(CommsDbContext db) =>
        new EmailTemplateConfigRepository(db);

    public static IConversationQueueRepository CreateQueueRepo(CommsDbContext db) =>
        new ConversationQueueRepository(db);

    public static IConversationAssignmentRepository CreateAssignmentRepo(CommsDbContext db) =>
        new ConversationAssignmentRepository(db);

    public static IConversationSlaStateRepository CreateSlaStateRepo(CommsDbContext db) =>
        new ConversationSlaStateRepository(db);

    public static IConversationSlaTriggerStateRepository CreateTriggerStateRepo(CommsDbContext db) =>
        new ConversationSlaTriggerStateRepository(db);

    public static IQueueEscalationConfigRepository CreateEscalationConfigRepo(CommsDbContext db) =>
        new QueueEscalationConfigRepository(db);

    public static IConversationTimelineRepository CreateTimelineRepo(CommsDbContext db) =>
        new ConversationTimelineRepository(db);

    public static IMessageMentionRepository CreateMentionRepo(CommsDbContext db) =>
        new MessageMentionRepository(db);

    public static IConversationTimelineService CreateTimelineService(CommsDbContext db) =>
        new ConversationTimelineService(
            CreateTimelineRepo(db),
            CreateLogger<ConversationTimelineService>());

    public static NoOpTimelineService CreateNoOpTimelineService() => new();

    public static IOperationalService CreateOperationalService(CommsDbContext db, NoOpAuditPublisher? audit = null, NoOpTimelineService? timeline = null) =>
        new OperationalService(
            CreateSlaStateRepo(db),
            CreateAssignmentRepo(db),
            CreateQueueRepo(db),
            CreateConversationRepo(db),
            timeline ?? new NoOpTimelineService(),
            audit ?? new NoOpAuditPublisher(),
            CreateLogger<OperationalService>());

    public static IEscalationTargetResolver CreateEscalationTargetResolver(
        CommsDbContext db, NoOpAuditPublisher? audit = null) =>
        new EscalationTargetResolver(
            CreateAssignmentRepo(db),
            CreateEscalationConfigRepo(db),
            audit ?? new NoOpAuditPublisher(),
            CreateLogger<EscalationTargetResolver>());

    public static ISlaNotificationService CreateSlaNotificationService(
        CommsDbContext db, MockNotificationsServiceClient? notif = null, NoOpAuditPublisher? audit = null, NoOpTimelineService? timeline = null)
    {
        var auditPub = audit ?? new NoOpAuditPublisher();
        return new SlaNotificationService(
            CreateSlaStateRepo(db),
            CreateTriggerStateRepo(db),
            CreateConversationRepo(db),
            CreateEscalationTargetResolver(db, auditPub),
            notif ?? new MockNotificationsServiceClient(),
            timeline ?? new NoOpTimelineService(),
            auditPub,
            CreateLogger<SlaNotificationService>());
    }

    public static IQueueEscalationConfigService CreateQueueEscalationConfigService(
        CommsDbContext db, NoOpAuditPublisher? audit = null) =>
        new QueueEscalationConfigService(
            CreateEscalationConfigRepo(db),
            CreateQueueRepo(db),
            audit ?? new NoOpAuditPublisher(),
            CreateLogger<QueueEscalationConfigService>());

    public static ILogger<T> CreateLogger<T>() =>
        LoggerFactory.Create(b => { }).CreateLogger<T>();
}

public class NoOpTimelineService : IConversationTimelineService
{
    public List<(Guid ConversationId, string EventType, string Summary)> Entries { get; } = new();

    public Task RecordAsync(
        Guid tenantId, Guid conversationId,
        string eventType, string actorType, string summary, string visibility,
        DateTime occurredAtUtc,
        string? eventSubType = null,
        Guid? actorId = null,
        string? actorDisplayName = null,
        string? metadataJson = null,
        Guid? relatedMessageId = null,
        Guid? relatedAssignmentId = null,
        Guid? relatedSlaId = null,
        CancellationToken ct = default)
    {
        Entries.Add((conversationId, eventType, summary));
        return Task.CompletedTask;
    }

    public Task<Comms.Application.DTOs.TimelinePageResponse> GetTimelineAsync(
        Guid tenantId, Guid conversationId,
        Comms.Application.DTOs.TimelineQuery query,
        CancellationToken ct = default)
    {
        return Task.FromResult(new Comms.Application.DTOs.TimelinePageResponse(
            new List<Comms.Application.DTOs.TimelineEntryResponse>(), 0, 1, 50, false));
    }
}

public class NoOpAuditPublisher : IAuditPublisher
{
    public List<(string EventType, string Action, string Description)> Events { get; } = new();

    public void Publish(
        string eventType, string action, string description,
        Guid tenantId, Guid? actorUserId = null,
        string? entityType = null, string? entityId = null,
        string? before = null, string? after = null,
        string? metadata = null)
    {
        Events.Add((eventType, action, description));
    }
}

public class NoOpMentionService : IMentionService
{
    public List<(Guid ConversationId, Guid MessageId, Guid SenderUserId)> ProcessedMentions { get; } = new();

    public Task ProcessMentionsAsync(
        Guid tenantId, Guid conversationId, Guid messageId,
        Guid senderUserId, string messageBody,
        CancellationToken ct = default)
    {
        ProcessedMentions.Add((conversationId, messageId, senderUserId));
        return Task.CompletedTask;
    }

    public Task<List<Comms.Application.DTOs.MentionResponse>> GetMentionsByMessageAsync(
        Guid tenantId, Guid messageId, CancellationToken ct = default)
    {
        return Task.FromResult(new List<Comms.Application.DTOs.MentionResponse>());
    }
}

public class MockNotificationsServiceClient : INotificationsServiceClient
{
    public List<OutboundEmailPayload> SentPayloads { get; } = new();
    public List<Application.DTOs.OperationalAlertPayload> SentAlerts { get; } = new();
    public NotificationsSendResult NextResult { get; set; } = new(
        Success: true,
        NotificationsRequestId: Guid.NewGuid(),
        ProviderUsed: "test-provider",
        ProviderMessageId: null,
        Status: "queued",
        ErrorMessage: null);

    public Task<NotificationsSendResult> SendEmailAsync(OutboundEmailPayload payload, CancellationToken ct = default)
    {
        SentPayloads.Add(payload);
        return Task.FromResult(NextResult);
    }

    public Task<NotificationsSendResult> SendOperationalAlertAsync(Application.DTOs.OperationalAlertPayload payload, CancellationToken ct = default)
    {
        SentAlerts.Add(payload);
        return Task.FromResult(NextResult);
    }
}

public class MockDocumentServiceClient : IDocumentServiceClient
{
    private readonly Dictionary<Guid, DocumentValidationResult> _results = new();

    public void SetResult(Guid documentId, DocumentValidationResult result) =>
        _results[documentId] = result;

    public Task<DocumentValidationResult> ValidateDocumentAsync(
        Guid documentId, Guid expectedTenantId, CancellationToken ct = default)
    {
        if (_results.TryGetValue(documentId, out var result))
            return Task.FromResult(result);

        return Task.FromResult(new DocumentValidationResult(false, null));
    }
}
