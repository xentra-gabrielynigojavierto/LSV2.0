using Comms.Application.DTOs;
using Comms.Application.Services;
using Comms.Domain.Constants;
using Comms.Domain.Enums;
using Xunit;

namespace Comms.Tests;

public class ConversationTimelineTests
{
    [Fact]
    public async Task RecordAsync_CreatesTimelineEntry()
    {
        var db = TestHelpers.CreateDbContext();
        var service = TestHelpers.CreateTimelineService(db);
        var conversationId = Guid.NewGuid();

        await service.RecordAsync(
            TestHelpers.TenantId, conversationId,
            TimelineEventTypes.MessageSent, TimelineActorType.User,
            "Message sent", TimelineVisibility.InternalOnly,
            DateTime.UtcNow,
            actorId: TestHelpers.UserId1,
            relatedMessageId: Guid.NewGuid(),
            metadataJson: "{\"channel\":\"InApp\"}");

        var result = await service.GetTimelineAsync(
            TestHelpers.TenantId, conversationId, new TimelineQuery());

        Assert.Single(result.Items);
        Assert.Equal(TimelineEventTypes.MessageSent, result.Items[0].EventType);
        Assert.Equal(TimelineActorType.User, result.Items[0].ActorType);
        Assert.Equal("Message sent", result.Items[0].Summary);
        Assert.Equal(TimelineVisibility.InternalOnly, result.Items[0].Visibility);
        Assert.Equal(TestHelpers.UserId1, result.Items[0].ActorId);
        Assert.Contains("InApp", result.Items[0].MetadataJson);
    }

    [Fact]
    public async Task GetTimeline_ReturnsDescendingOrder()
    {
        var db = TestHelpers.CreateDbContext();
        var service = TestHelpers.CreateTimelineService(db);
        var conversationId = Guid.NewGuid();

        var t1 = DateTime.UtcNow.AddMinutes(-30);
        var t2 = DateTime.UtcNow.AddMinutes(-20);
        var t3 = DateTime.UtcNow.AddMinutes(-10);

        await service.RecordAsync(TestHelpers.TenantId, conversationId,
            TimelineEventTypes.SlaStarted, TimelineActorType.System, "SLA started",
            TimelineVisibility.InternalOnly, t1);
        await service.RecordAsync(TestHelpers.TenantId, conversationId,
            TimelineEventTypes.Assigned, TimelineActorType.User, "Assigned",
            TimelineVisibility.InternalOnly, t2);
        await service.RecordAsync(TestHelpers.TenantId, conversationId,
            TimelineEventTypes.MessageSent, TimelineActorType.User, "Message",
            TimelineVisibility.SharedExternalSafe, t3);

        var result = await service.GetTimelineAsync(
            TestHelpers.TenantId, conversationId, new TimelineQuery());

        Assert.Equal(3, result.Items.Count);
        Assert.True(result.Items[0].OccurredAtUtc >= result.Items[1].OccurredAtUtc);
        Assert.True(result.Items[1].OccurredAtUtc >= result.Items[2].OccurredAtUtc);
    }

    [Fact]
    public async Task GetTimeline_VisibilityFiltering_ExcludesInternalOnly()
    {
        var db = TestHelpers.CreateDbContext();
        var service = TestHelpers.CreateTimelineService(db);
        var conversationId = Guid.NewGuid();

        await service.RecordAsync(TestHelpers.TenantId, conversationId,
            TimelineEventTypes.MessageSent, TimelineActorType.User, "Internal msg",
            TimelineVisibility.InternalOnly, DateTime.UtcNow);
        await service.RecordAsync(TestHelpers.TenantId, conversationId,
            TimelineEventTypes.EmailReceived, TimelineActorType.System, "Email received",
            TimelineVisibility.SharedExternalSafe, DateTime.UtcNow);
        await service.RecordAsync(TestHelpers.TenantId, conversationId,
            TimelineEventTypes.StatusChanged, TimelineActorType.User, "Status changed",
            TimelineVisibility.InternalOnly, DateTime.UtcNow);

        var externalResult = await service.GetTimelineAsync(
            TestHelpers.TenantId, conversationId,
            new TimelineQuery(IncludeInternal: false));

        Assert.Single(externalResult.Items);
        Assert.Equal(TimelineEventTypes.EmailReceived, externalResult.Items[0].EventType);

        var internalResult = await service.GetTimelineAsync(
            TestHelpers.TenantId, conversationId,
            new TimelineQuery(IncludeInternal: true));

        Assert.Equal(3, internalResult.Items.Count);
    }

    [Fact]
    public async Task GetTimeline_EventTypeFiltering()
    {
        var db = TestHelpers.CreateDbContext();
        var service = TestHelpers.CreateTimelineService(db);
        var conversationId = Guid.NewGuid();

        await service.RecordAsync(TestHelpers.TenantId, conversationId,
            TimelineEventTypes.MessageSent, TimelineActorType.User, "Message",
            TimelineVisibility.InternalOnly, DateTime.UtcNow);
        await service.RecordAsync(TestHelpers.TenantId, conversationId,
            TimelineEventTypes.Assigned, TimelineActorType.User, "Assigned",
            TimelineVisibility.InternalOnly, DateTime.UtcNow);
        await service.RecordAsync(TestHelpers.TenantId, conversationId,
            TimelineEventTypes.PriorityChanged, TimelineActorType.User, "Priority",
            TimelineVisibility.InternalOnly, DateTime.UtcNow);

        var filtered = await service.GetTimelineAsync(
            TestHelpers.TenantId, conversationId,
            new TimelineQuery(EventTypes: new List<string> { TimelineEventTypes.MessageSent, TimelineEventTypes.Assigned }));

        Assert.Equal(2, filtered.Items.Count);
        Assert.DoesNotContain(filtered.Items, i => i.EventType == TimelineEventTypes.PriorityChanged);
    }

    [Fact]
    public async Task GetTimeline_Pagination_WorksCorrectly()
    {
        var db = TestHelpers.CreateDbContext();
        var service = TestHelpers.CreateTimelineService(db);
        var conversationId = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
        {
            await service.RecordAsync(TestHelpers.TenantId, conversationId,
                TimelineEventTypes.MessageSent, TimelineActorType.User, $"Message {i}",
                TimelineVisibility.InternalOnly, DateTime.UtcNow.AddMinutes(-i));
        }

        var page1 = await service.GetTimelineAsync(
            TestHelpers.TenantId, conversationId,
            new TimelineQuery(Page: 1, PageSize: 2));

        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(5, page1.TotalCount);
        Assert.True(page1.HasMore);
        Assert.Equal(1, page1.Page);
        Assert.Equal(2, page1.PageSize);

        var page3 = await service.GetTimelineAsync(
            TestHelpers.TenantId, conversationId,
            new TimelineQuery(Page: 3, PageSize: 2));

        Assert.Single(page3.Items);
        Assert.False(page3.HasMore);
    }

    [Fact]
    public async Task GetTimeline_DateRangeFiltering()
    {
        var db = TestHelpers.CreateDbContext();
        var service = TestHelpers.CreateTimelineService(db);
        var conversationId = Guid.NewGuid();

        var now = DateTime.UtcNow;
        await service.RecordAsync(TestHelpers.TenantId, conversationId,
            TimelineEventTypes.SlaStarted, TimelineActorType.System, "SLA started",
            TimelineVisibility.InternalOnly, now.AddHours(-3));
        await service.RecordAsync(TestHelpers.TenantId, conversationId,
            TimelineEventTypes.MessageSent, TimelineActorType.User, "Message",
            TimelineVisibility.InternalOnly, now.AddHours(-1));
        await service.RecordAsync(TestHelpers.TenantId, conversationId,
            TimelineEventTypes.Assigned, TimelineActorType.User, "Assigned",
            TimelineVisibility.InternalOnly, now);

        var filtered = await service.GetTimelineAsync(
            TestHelpers.TenantId, conversationId,
            new TimelineQuery(FromDate: now.AddHours(-2), ToDate: now.AddMinutes(-30)));

        Assert.Single(filtered.Items);
        Assert.Equal(TimelineEventTypes.MessageSent, filtered.Items[0].EventType);
    }

    [Fact]
    public async Task GetTimeline_TenantIsolation()
    {
        var db = TestHelpers.CreateDbContext();
        var service = TestHelpers.CreateTimelineService(db);
        var conversationId = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();

        await service.RecordAsync(TestHelpers.TenantId, conversationId,
            TimelineEventTypes.MessageSent, TimelineActorType.User, "Our message",
            TimelineVisibility.InternalOnly, DateTime.UtcNow);
        await service.RecordAsync(otherTenant, conversationId,
            TimelineEventTypes.MessageSent, TimelineActorType.User, "Their message",
            TimelineVisibility.InternalOnly, DateTime.UtcNow);

        var result = await service.GetTimelineAsync(
            TestHelpers.TenantId, conversationId, new TimelineQuery());

        Assert.Single(result.Items);
        Assert.Equal("Our message", result.Items[0].Summary);
    }

    [Fact]
    public async Task RecordAsync_AllRelatedIds_Persisted()
    {
        var db = TestHelpers.CreateDbContext();
        var service = TestHelpers.CreateTimelineService(db);
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var slaId = Guid.NewGuid();

        await service.RecordAsync(
            TestHelpers.TenantId, conversationId,
            TimelineEventTypes.Assigned, TimelineActorType.User, "Assigned",
            TimelineVisibility.InternalOnly, DateTime.UtcNow,
            actorId: TestHelpers.UserId1,
            actorDisplayName: "John Doe",
            relatedMessageId: messageId,
            relatedAssignmentId: assignmentId,
            relatedSlaId: slaId);

        var result = await service.GetTimelineAsync(
            TestHelpers.TenantId, conversationId, new TimelineQuery());

        var entry = Assert.Single(result.Items);
        Assert.Equal(messageId, entry.RelatedMessageId);
        Assert.Equal(assignmentId, entry.RelatedAssignmentId);
        Assert.Equal(slaId, entry.RelatedSlaId);
        Assert.Equal("John Doe", entry.ActorDisplayName);
    }

    [Fact]
    public async Task RecordAsync_EventSubType_Persisted()
    {
        var db = TestHelpers.CreateDbContext();
        var service = TestHelpers.CreateTimelineService(db);
        var conversationId = Guid.NewGuid();

        await service.RecordAsync(
            TestHelpers.TenantId, conversationId,
            TimelineEventTypes.EscalationTriggered, TimelineActorType.System, "Escalation",
            TimelineVisibility.InternalOnly, DateTime.UtcNow,
            eventSubType: "FIRST_RESPONSE");

        var result = await service.GetTimelineAsync(
            TestHelpers.TenantId, conversationId, new TimelineQuery());

        Assert.Equal("FIRST_RESPONSE", result.Items[0].EventSubType);
    }

    [Fact]
    public async Task MessageService_RecordsTimelineEntry_OnAdd()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var audit = new NoOpAuditPublisher();
        var timeline = new NoOpTimelineService();

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(participant);

        var msgService = new MessageService(
            messageRepo, conversationRepo, participantRepo,
            timeline, new NoOpMentionService(), audit, TestHelpers.CreateLogger<MessageService>());

        var request = new AddMessageRequest("Test message", Channel.InApp, Direction.Internal, VisibilityType.InternalOnly);
        await msgService.AddAsync(TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1, conversation.Id, request);

        Assert.Single(timeline.Entries);
        Assert.Equal(conversation.Id, timeline.Entries[0].ConversationId);
        Assert.Equal(TimelineEventTypes.MessageSent, timeline.Entries[0].EventType);
    }

    [Fact]
    public async Task AssignmentService_RecordsTimelineEntries()
    {
        var db = TestHelpers.CreateDbContext();
        var audit = new NoOpAuditPublisher();
        var timeline = new NoOpTimelineService();

        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var queueRepo = TestHelpers.CreateQueueRepo(db);
        var slaRepo = TestHelpers.CreateSlaStateRepo(db);

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var queue = Comms.Domain.Entities.ConversationQueue.Create(
            TestHelpers.TenantId, "Test Queue", "TEST", null, true, TestHelpers.UserId1);
        await queueRepo.AddAsync(queue);

        var assignmentService = new AssignmentService(
            TestHelpers.CreateAssignmentRepo(db), queueRepo, conversationRepo, slaRepo,
            timeline, audit, TestHelpers.CreateLogger<AssignmentService>());

        await assignmentService.AssignAsync(
            TestHelpers.TenantId, conversation.Id, TestHelpers.UserId1,
            new AssignConversationRequest(queue.Id, TestHelpers.UserId2, ConversationPriority.Normal));

        Assert.Single(timeline.Entries);
        Assert.Equal(TimelineEventTypes.Assigned, timeline.Entries[0].EventType);

        await assignmentService.ReassignAsync(
            TestHelpers.TenantId, conversation.Id, TestHelpers.UserId1,
            new ReassignConversationRequest(queue.Id, TestHelpers.UserId1));

        Assert.Equal(2, timeline.Entries.Count);
        Assert.Equal(TimelineEventTypes.Reassigned, timeline.Entries[1].EventType);

        await assignmentService.UnassignAsync(
            TestHelpers.TenantId, conversation.Id, TestHelpers.UserId1);

        Assert.Equal(3, timeline.Entries.Count);
        Assert.Equal(TimelineEventTypes.Unassigned, timeline.Entries[2].EventType);
    }

    [Fact]
    public async Task SlaNotificationService_RecordsTimelineEntry_OnTrigger()
    {
        var db = TestHelpers.CreateDbContext();
        var notif = new MockNotificationsServiceClient();
        var audit = new NoOpAuditPublisher();
        var timeline = new NoOpTimelineService();

        var conversation = TestHelpers.CreateTestConversation();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        await conversationRepo.AddAsync(conversation);

        var queueRepo = TestHelpers.CreateQueueRepo(db);
        var queue = Comms.Domain.Entities.ConversationQueue.Create(
            TestHelpers.TenantId, "Escalation Queue", "ESC", null, true, TestHelpers.UserId1);
        await queueRepo.AddAsync(queue);

        var assignmentRepo = TestHelpers.CreateAssignmentRepo(db);
        var assignment = Comms.Domain.Entities.ConversationAssignment.Create(
            TestHelpers.TenantId, conversation.Id, queue.Id, TestHelpers.UserId1, TestHelpers.UserId1, TestHelpers.UserId1);
        await assignmentRepo.AddAsync(assignment);

        var escalationConfigRepo = TestHelpers.CreateEscalationConfigRepo(db);
        var escalationConfig = Comms.Domain.Entities.QueueEscalationConfig.Create(
            TestHelpers.TenantId, queue.Id, TestHelpers.UserId1, TestHelpers.UserId1);
        await escalationConfigRepo.AddAsync(escalationConfig);

        var slaRepo = TestHelpers.CreateSlaStateRepo(db);
        var startTime = DateTime.UtcNow.AddHours(-48);
        var sla = Comms.Domain.Entities.ConversationSlaState.Initialize(
            TestHelpers.TenantId, conversation.Id, ConversationPriority.High, startTime, TestHelpers.UserId1);
        await slaRepo.AddAsync(sla);

        var triggerRepo = TestHelpers.CreateTriggerStateRepo(db);

        var slaService = new SlaNotificationService(
            slaRepo, triggerRepo, conversationRepo,
            TestHelpers.CreateEscalationTargetResolver(db, audit),
            notif, timeline, audit,
            TestHelpers.CreateLogger<SlaNotificationService>());

        var systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        await slaService.EvaluateAllAsync(TestHelpers.TenantId, systemUserId);

        Assert.True(timeline.Entries.Count > 0);
        Assert.Contains(timeline.Entries,
            e => e.EventType == TimelineEventTypes.FirstResponseBreach ||
                 e.EventType == TimelineEventTypes.ResolutionBreach);
    }

    [Fact]
    public async Task PageSizeClamping_EnforcedLimits()
    {
        var db = TestHelpers.CreateDbContext();
        var service = TestHelpers.CreateTimelineService(db);
        var conversationId = Guid.NewGuid();

        await service.RecordAsync(TestHelpers.TenantId, conversationId,
            TimelineEventTypes.MessageSent, TimelineActorType.User, "Test",
            TimelineVisibility.InternalOnly, DateTime.UtcNow);

        var resultMax = await service.GetTimelineAsync(
            TestHelpers.TenantId, conversationId,
            new TimelineQuery(PageSize: 300));

        Assert.Equal(200, resultMax.PageSize);

        var resultMin = await service.GetTimelineAsync(
            TestHelpers.TenantId, conversationId,
            new TimelineQuery(PageSize: 0));

        Assert.Equal(1, resultMin.PageSize);
    }

    [Fact]
    public async Task EmptyTimeline_ReturnsEmptyPage()
    {
        var db = TestHelpers.CreateDbContext();
        var service = TestHelpers.CreateTimelineService(db);
        var conversationId = Guid.NewGuid();

        var result = await service.GetTimelineAsync(
            TestHelpers.TenantId, conversationId, new TimelineQuery());

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task ConversationService_RecordsTimelineEntry_OnStatusChange()
    {
        var db = TestHelpers.CreateDbContext();
        var audit = new NoOpAuditPublisher();
        var timeline = new NoOpTimelineService();

        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var readStateRepo = TestHelpers.CreateReadStateRepo(db);
        var attachmentRepo = TestHelpers.CreateAttachmentRepo(db);

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(participant);

        var convService = new ConversationService(
            conversationRepo, participantRepo, messageRepo, readStateRepo, attachmentRepo,
            TestHelpers.CreateOperationalService(db, audit),
            timeline, audit, TestHelpers.CreateLogger<ConversationService>());

        await convService.UpdateStatusAsync(
            TestHelpers.TenantId, conversation.Id, TestHelpers.UserId1,
            new UpdateConversationStatusRequest(ConversationStatus.Open));

        Assert.Single(timeline.Entries);
        Assert.Equal(TimelineEventTypes.StatusChanged, timeline.Entries[0].EventType);
        Assert.Contains("New", timeline.Entries[0].Summary);
        Assert.Contains("Open", timeline.Entries[0].Summary);
    }

    [Fact]
    public async Task OperationalService_RecordsTimelineEntry_OnPriorityChange()
    {
        var db = TestHelpers.CreateDbContext();
        var audit = new NoOpAuditPublisher();
        var timeline = new NoOpTimelineService();

        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var slaRepo = TestHelpers.CreateSlaStateRepo(db);
        var sla = Comms.Domain.Entities.ConversationSlaState.Initialize(
            TestHelpers.TenantId, conversation.Id, ConversationPriority.Normal,
            DateTime.UtcNow.AddMinutes(-30), TestHelpers.UserId1);
        await slaRepo.AddAsync(sla);

        var opService = new OperationalService(
            slaRepo, TestHelpers.CreateAssignmentRepo(db),
            TestHelpers.CreateQueueRepo(db), conversationRepo,
            timeline, audit, TestHelpers.CreateLogger<OperationalService>());

        await opService.UpdatePriorityAsync(
            TestHelpers.TenantId, conversation.Id, TestHelpers.UserId1,
            new UpdateConversationPriorityRequest(ConversationPriority.High));

        Assert.Single(timeline.Entries);
        Assert.Equal(TimelineEventTypes.PriorityChanged, timeline.Entries[0].EventType);
        Assert.Contains("Normal", timeline.Entries[0].Summary);
        Assert.Contains("High", timeline.Entries[0].Summary);
    }

    [Fact]
    public async Task OperationalService_RecordsTimelineEntry_OnSlaInitialize()
    {
        var db = TestHelpers.CreateDbContext();
        var audit = new NoOpAuditPublisher();
        var timeline = new NoOpTimelineService();

        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var opService = new OperationalService(
            TestHelpers.CreateSlaStateRepo(db), TestHelpers.CreateAssignmentRepo(db),
            TestHelpers.CreateQueueRepo(db), conversationRepo,
            timeline, audit, TestHelpers.CreateLogger<OperationalService>());

        await opService.InitializeSlaAsync(
            TestHelpers.TenantId, conversation.Id,
            ConversationPriority.Normal, DateTime.UtcNow,
            TestHelpers.UserId1);

        Assert.Single(timeline.Entries);
        Assert.Equal(TimelineEventTypes.SlaStarted, timeline.Entries[0].EventType);
    }

    [Fact]
    public void TimelineConstants_HaveExpectedValues()
    {
        Assert.Equal("MESSAGE_SENT", TimelineEventTypes.MessageSent);
        Assert.Equal("EMAIL_RECEIVED", TimelineEventTypes.EmailReceived);
        Assert.Equal("EMAIL_SENT", TimelineEventTypes.EmailSent);
        Assert.Equal("ASSIGNED", TimelineEventTypes.Assigned);
        Assert.Equal("REASSIGNED", TimelineEventTypes.Reassigned);
        Assert.Equal("UNASSIGNED", TimelineEventTypes.Unassigned);
        Assert.Equal("PRIORITY_CHANGED", TimelineEventTypes.PriorityChanged);
        Assert.Equal("STATUS_CHANGED", TimelineEventTypes.StatusChanged);
        Assert.Equal("SLA_STARTED", TimelineEventTypes.SlaStarted);
        Assert.Equal("FIRST_RESPONSE_SATISFIED", TimelineEventTypes.FirstResponseSatisfied);
        Assert.Equal("FIRST_RESPONSE_WARNING", TimelineEventTypes.FirstResponseWarning);
        Assert.Equal("FIRST_RESPONSE_BREACH", TimelineEventTypes.FirstResponseBreach);
        Assert.Equal("RESOLUTION_WARNING", TimelineEventTypes.ResolutionWarning);
        Assert.Equal("RESOLUTION_BREACH", TimelineEventTypes.ResolutionBreach);
        Assert.Equal("RESOLVED", TimelineEventTypes.Resolved);
        Assert.Equal("ESCALATION_TRIGGERED", TimelineEventTypes.EscalationTriggered);

        Assert.Equal("USER", TimelineActorType.User);
        Assert.Equal("SYSTEM", TimelineActorType.System);

        Assert.Equal("INTERNAL_ONLY", TimelineVisibility.InternalOnly);
        Assert.Equal("SHARED_EXTERNAL_SAFE", TimelineVisibility.SharedExternalSafe);
    }
}
