using Comms.Application.DTOs;
using Comms.Application.Services;
using Comms.Domain.Enums;
using Xunit;

namespace Comms.Tests;

public class ParticipantAccessTests
{
    [Fact]
    public async Task NonParticipant_CannotAccessConversation()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var readStateRepo = TestHelpers.CreateReadStateRepo(db);
        var audit = new NoOpAuditPublisher();

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var nonParticipantUserId = Guid.NewGuid();

        var attachmentRepo = TestHelpers.CreateAttachmentRepo(db);
        var service = new ConversationService(
            conversationRepo, participantRepo, messageRepo, readStateRepo, attachmentRepo,
            TestHelpers.CreateOperationalService(db, audit),
            new NoOpTimelineService(), audit, TestHelpers.CreateLogger<ConversationService>());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await service.GetThreadAsync(TestHelpers.TenantId, conversation.Id, nonParticipantUserId));
    }

    [Fact]
    public async Task InactiveParticipant_CannotPostMessage()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var audit = new NoOpAuditPublisher();

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(participant);
        participant.Deactivate(TestHelpers.UserId1);
        await participantRepo.UpdateAsync(participant);

        var msgService = new MessageService(
            messageRepo, conversationRepo, participantRepo,
            new NoOpTimelineService(), new NoOpMentionService(), audit, TestHelpers.CreateLogger<MessageService>());

        var request = new AddMessageRequest("Hello", Channel.InApp, Direction.Internal, VisibilityType.InternalOnly);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await msgService.AddAsync(TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1, conversation.Id, request));
    }

    [Fact]
    public async Task ParticipantWithCanReplyFalse_CannotPostMessage()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var audit = new NoOpAuditPublisher();

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1, canReply: false);
        await participantRepo.AddAsync(participant);

        var msgService = new MessageService(
            messageRepo, conversationRepo, participantRepo,
            new NoOpTimelineService(), new NoOpMentionService(), audit, TestHelpers.CreateLogger<MessageService>());

        var request = new AddMessageRequest("Hello", Channel.InApp, Direction.Internal, VisibilityType.InternalOnly);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await msgService.AddAsync(TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1, conversation.Id, request));
    }
}
