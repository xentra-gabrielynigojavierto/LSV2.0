using Xunit;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Services;
using Comms.Domain.Constants;
using Comms.Domain.Enums;
using Comms.Infrastructure.Repositories;

namespace Comms.Tests;

public class MentionTests
{
    [Fact]
    public void MentionParser_ExtractsValidGuids()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var body = $"Hey @{{{id1}}} and @{{{id2}}}, check this out";

        var result = MentionParser.ExtractMentionedUserIds(body);

        Assert.Equal(2, result.Count);
        Assert.Contains(id1, result);
        Assert.Contains(id2, result);
    }

    [Fact]
    public void MentionParser_ReturnsEmptyForNoMentions()
    {
        var result = MentionParser.ExtractMentionedUserIds("No mentions here");
        Assert.Empty(result);
    }

    [Fact]
    public void MentionParser_ReturnsEmptyForNullOrWhitespace()
    {
        Assert.Empty(MentionParser.ExtractMentionedUserIds(""));
        Assert.Empty(MentionParser.ExtractMentionedUserIds("   "));
        Assert.Empty(MentionParser.ExtractMentionedUserIds(null!));
    }

    [Fact]
    public void MentionParser_DeduplicatesSameUser()
    {
        var id = Guid.NewGuid();
        var body = $"@{{{id}}} and @{{{id}}} again";

        var result = MentionParser.ExtractMentionedUserIds(body);

        Assert.Single(result);
        Assert.Equal(id, result[0]);
    }

    [Fact]
    public void MentionParser_CapsAtMaxMentions()
    {
        var ids = Enumerable.Range(0, 15).Select(_ => Guid.NewGuid()).ToList();
        var body = string.Join(" ", ids.Select(id => $"@{{{id}}}"));

        var result = MentionParser.ExtractMentionedUserIds(body);

        Assert.Equal(MentionParser.MaxMentionsPerMessage, result.Count);
    }

    [Fact]
    public void MentionParser_IgnoresInvalidGuids()
    {
        var body = "@{not-a-guid} @{12345678-1234-1234-1234-123456789abc}";

        var result = MentionParser.ExtractMentionedUserIds(body);

        Assert.Single(result);
        Assert.Equal(Guid.Parse("12345678-1234-1234-1234-123456789abc"), result[0]);
    }

    [Fact]
    public async Task MentionService_ProcessesMentionsAndPersists()
    {
        var db = TestHelpers.CreateDbContext();
        var mentionRepo = new MessageMentionRepository(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var timeline = new NoOpTimelineService();
        var notif = new MockNotificationsServiceClient();

        var conversation = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conversation);

        var mentionedUserId = Guid.NewGuid();
        var participant = TestHelpers.CreateTestParticipant(conversation.Id, mentionedUserId);
        await participantRepo.AddAsync(participant);

        var senderParticipant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(senderParticipant);

        var service = new MentionService(
            mentionRepo, participantRepo, timeline, notif,
            TestHelpers.CreateLogger<MentionService>());

        var messageId = Guid.NewGuid();
        var body = $"Hey @{{{mentionedUserId}}}, check this case";

        await service.ProcessMentionsAsync(
            TestHelpers.TenantId, conversation.Id, messageId,
            TestHelpers.UserId1, body);

        var mentions = await mentionRepo.ListByMessageAsync(TestHelpers.TenantId, messageId);
        Assert.Single(mentions);
        Assert.Equal(mentionedUserId, mentions[0].MentionedUserId);
        Assert.Equal(TestHelpers.UserId1, mentions[0].MentionedByUserId);
        Assert.True(mentions[0].IsMentionedUserParticipant);
    }

    [Fact]
    public async Task MentionService_RemovesSelfMention()
    {
        var db = TestHelpers.CreateDbContext();
        var mentionRepo = new MessageMentionRepository(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var timeline = new NoOpTimelineService();
        var notif = new MockNotificationsServiceClient();

        var conversation = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conversation);

        var senderParticipant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(senderParticipant);

        var service = new MentionService(
            mentionRepo, participantRepo, timeline, notif,
            TestHelpers.CreateLogger<MentionService>());

        var messageId = Guid.NewGuid();
        var body = $"@{{{TestHelpers.UserId1}}} self-mention";

        await service.ProcessMentionsAsync(
            TestHelpers.TenantId, conversation.Id, messageId,
            TestHelpers.UserId1, body);

        var mentions = await mentionRepo.ListByMessageAsync(TestHelpers.TenantId, messageId);
        Assert.Empty(mentions);
    }

    [Fact]
    public async Task MentionService_MarksNonParticipantCorrectly_SkipsNotification()
    {
        var db = TestHelpers.CreateDbContext();
        var mentionRepo = new MessageMentionRepository(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var timeline = new NoOpTimelineService();
        var notif = new MockNotificationsServiceClient();

        var conversation = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conversation);

        var senderParticipant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(senderParticipant);

        var nonParticipantUserId = Guid.NewGuid();

        var service = new MentionService(
            mentionRepo, participantRepo, timeline, notif,
            TestHelpers.CreateLogger<MentionService>());

        var messageId = Guid.NewGuid();
        var body = $"@{{{nonParticipantUserId}}} heads up";

        await service.ProcessMentionsAsync(
            TestHelpers.TenantId, conversation.Id, messageId,
            TestHelpers.UserId1, body);

        var mentions = await mentionRepo.ListByMessageAsync(TestHelpers.TenantId, messageId);
        Assert.Single(mentions);
        Assert.False(mentions[0].IsMentionedUserParticipant);

        Assert.Single(timeline.Entries);
        Assert.Empty(notif.SentAlerts);
    }

    [Fact]
    public async Task MentionService_SendsNotificationForMention()
    {
        var db = TestHelpers.CreateDbContext();
        var mentionRepo = new MessageMentionRepository(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var timeline = new NoOpTimelineService();
        var notif = new MockNotificationsServiceClient();

        var conversation = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conversation);

        var mentionedUserId = Guid.NewGuid();
        var participant = TestHelpers.CreateTestParticipant(conversation.Id, mentionedUserId);
        await participantRepo.AddAsync(participant);

        var senderParticipant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(senderParticipant);

        var service = new MentionService(
            mentionRepo, participantRepo, timeline, notif,
            TestHelpers.CreateLogger<MentionService>());

        var messageId = Guid.NewGuid();
        var body = $"@{{{mentionedUserId}}} please review";

        await service.ProcessMentionsAsync(
            TestHelpers.TenantId, conversation.Id, messageId,
            TestHelpers.UserId1, body);

        Assert.Single(notif.SentAlerts);
        var alert = notif.SentAlerts[0];
        Assert.Equal("comms_internal_mention", alert.TriggerType);
        Assert.Equal(mentionedUserId, alert.TargetUserId);
        Assert.Equal($"mention-{messageId}-{mentionedUserId}", alert.IdempotencyKey);
    }

    [Fact]
    public async Task MentionService_RecordsTimelineEntry()
    {
        var db = TestHelpers.CreateDbContext();
        var mentionRepo = new MessageMentionRepository(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var timeline = new NoOpTimelineService();
        var notif = new MockNotificationsServiceClient();

        var conversation = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conversation);

        var mentionedUserId = Guid.NewGuid();
        var participant = TestHelpers.CreateTestParticipant(conversation.Id, mentionedUserId);
        await participantRepo.AddAsync(participant);

        var senderParticipant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(senderParticipant);

        var service = new MentionService(
            mentionRepo, participantRepo, timeline, notif,
            TestHelpers.CreateLogger<MentionService>());

        var messageId = Guid.NewGuid();
        var body = $"@{{{mentionedUserId}}} take a look";

        await service.ProcessMentionsAsync(
            TestHelpers.TenantId, conversation.Id, messageId,
            TestHelpers.UserId1, body);

        Assert.Single(timeline.Entries);
        Assert.Equal(TimelineEventTypes.Mentioned, timeline.Entries[0].EventType);
    }

    [Fact]
    public async Task MentionService_NoMentions_DoesNothing()
    {
        var db = TestHelpers.CreateDbContext();
        var mentionRepo = new MessageMentionRepository(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var timeline = new NoOpTimelineService();
        var notif = new MockNotificationsServiceClient();

        var service = new MentionService(
            mentionRepo, participantRepo, timeline, notif,
            TestHelpers.CreateLogger<MentionService>());

        var messageId = Guid.NewGuid();
        await service.ProcessMentionsAsync(
            TestHelpers.TenantId, Guid.NewGuid(), messageId,
            TestHelpers.UserId1, "No mentions here");

        Assert.Empty(timeline.Entries);
        Assert.Empty(notif.SentAlerts);
    }

    [Fact]
    public async Task MentionService_GetMentionsByMessage_ReturnsMentionResponses()
    {
        var db = TestHelpers.CreateDbContext();
        var mentionRepo = new MessageMentionRepository(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var timeline = new NoOpTimelineService();
        var notif = new MockNotificationsServiceClient();

        var conversation = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conversation);

        var mentionedUserId = Guid.NewGuid();
        var participant = TestHelpers.CreateTestParticipant(conversation.Id, mentionedUserId);
        await participantRepo.AddAsync(participant);

        var senderParticipant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(senderParticipant);

        var service = new MentionService(
            mentionRepo, participantRepo, timeline, notif,
            TestHelpers.CreateLogger<MentionService>());

        var messageId = Guid.NewGuid();
        var body = $"@{{{mentionedUserId}}} check this";

        await service.ProcessMentionsAsync(
            TestHelpers.TenantId, conversation.Id, messageId,
            TestHelpers.UserId1, body);

        var results = await service.GetMentionsByMessageAsync(TestHelpers.TenantId, messageId);
        Assert.Single(results);
        Assert.Equal(mentionedUserId, results[0].MentionedUserId);
        Assert.Equal(TestHelpers.UserId1, results[0].MentionedByUserId);
        Assert.True(results[0].IsMentionedUserParticipant);
    }

    [Fact]
    public async Task MentionService_MultipleMentions_AllProcessed()
    {
        var db = TestHelpers.CreateDbContext();
        var mentionRepo = new MessageMentionRepository(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var timeline = new NoOpTimelineService();
        var notif = new MockNotificationsServiceClient();

        var conversation = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conversation);

        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();
        await participantRepo.AddAsync(TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1));
        await participantRepo.AddAsync(TestHelpers.CreateTestParticipant(conversation.Id, user2));
        await participantRepo.AddAsync(TestHelpers.CreateTestParticipant(conversation.Id, user3));

        var service = new MentionService(
            mentionRepo, participantRepo, timeline, notif,
            TestHelpers.CreateLogger<MentionService>());

        var messageId = Guid.NewGuid();
        var body = $"@{{{user2}}} and @{{{user3}}} please advise";

        await service.ProcessMentionsAsync(
            TestHelpers.TenantId, conversation.Id, messageId,
            TestHelpers.UserId1, body);

        var mentions = await mentionRepo.ListByMessageAsync(TestHelpers.TenantId, messageId);
        Assert.Equal(2, mentions.Count);
        Assert.Equal(2, notif.SentAlerts.Count);
        Assert.Equal(2, timeline.Entries.Count);
    }

    [Fact]
    public async Task MessageResponse_PopulatesMentions_FromBody()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var timeline = new NoOpTimelineService();
        var mentionService = new NoOpMentionService();
        var audit = new NoOpAuditPublisher();

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(participant);

        var msgService = new MessageService(
            messageRepo, conversationRepo, participantRepo,
            timeline, mentionService, audit, TestHelpers.CreateLogger<MessageService>());

        var mentionedId = Guid.NewGuid();
        var request = new AddMessageRequest(
            $"Hey @{{{mentionedId}}} check this",
            Channel.InApp, Direction.Internal, VisibilityType.InternalOnly);

        var response = await msgService.AddAsync(
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1,
            conversation.Id, request);

        Assert.NotNull(response.Mentions);
        Assert.Single(response.Mentions);
        Assert.Contains(mentionedId, response.Mentions);
    }

    [Fact]
    public async Task MessageResponse_NullMentions_WhenNoMentionsInBody()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var timeline = new NoOpTimelineService();
        var mentionService = new NoOpMentionService();
        var audit = new NoOpAuditPublisher();

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(participant);

        var msgService = new MessageService(
            messageRepo, conversationRepo, participantRepo,
            timeline, mentionService, audit, TestHelpers.CreateLogger<MessageService>());

        var request = new AddMessageRequest(
            "No mentions here",
            Channel.InApp, Direction.Internal, VisibilityType.InternalOnly);

        var response = await msgService.AddAsync(
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1,
            conversation.Id, request);

        Assert.Null(response.Mentions);
    }

    [Fact]
    public async Task MessageService_IntegratesMentions_ViaAddAsync()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var timeline = new NoOpTimelineService();
        var mentionService = new NoOpMentionService();
        var audit = new NoOpAuditPublisher();

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(participant);

        var msgService = new MessageService(
            messageRepo, conversationRepo, participantRepo,
            timeline, mentionService, audit, TestHelpers.CreateLogger<MessageService>());

        var mentionedId = Guid.NewGuid();
        var request = new AddMessageRequest(
            $"Hey @{{{mentionedId}}} check this",
            Channel.InApp, Direction.Internal, VisibilityType.InternalOnly);

        await msgService.AddAsync(
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1,
            conversation.Id, request);

        Assert.Single(mentionService.ProcessedMentions);
        Assert.Equal(conversation.Id, mentionService.ProcessedMentions[0].ConversationId);
    }
}
