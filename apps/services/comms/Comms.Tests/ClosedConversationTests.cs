using Comms.Application.DTOs;
using Comms.Application.Services;
using Comms.Domain.Enums;
using Xunit;

namespace Comms.Tests;

public class ClosedConversationTests
{
    [Fact]
    public async Task NewMessage_OnNewConversation_AutoTransitionsToOpen()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var audit = new NoOpAuditPublisher();

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);
        Assert.Equal(ConversationStatus.New, conversation.Status);

        var participant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(participant);

        var msgService = new MessageService(
            messageRepo, conversationRepo, participantRepo,
            new NoOpTimelineService(), new NoOpMentionService(), audit, TestHelpers.CreateLogger<MessageService>());

        var request = new AddMessageRequest("First message", Channel.InApp, Direction.Internal, VisibilityType.InternalOnly);
        await msgService.AddAsync(TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1, conversation.Id, request);

        var updated = await conversationRepo.GetByIdAsync(TestHelpers.TenantId, conversation.Id);
        Assert.Equal(ConversationStatus.Open, updated!.Status);
    }

    [Fact]
    public void Conversation_ReopenFromClosed_TransitionsToOpen()
    {
        var conversation = TestHelpers.CreateTestConversation();
        conversation.UpdateStatus(ConversationStatus.Open, TestHelpers.UserId1);
        conversation.UpdateStatus(ConversationStatus.Closed, TestHelpers.UserId1);
        Assert.Equal(ConversationStatus.Closed, conversation.Status);

        conversation.ReopenFromClosed(TestHelpers.UserId1);
        Assert.Equal(ConversationStatus.Open, conversation.Status);
    }

    [Fact]
    public void ReopenFromClosed_WhenNotClosed_DoesNothing()
    {
        var conversation = TestHelpers.CreateTestConversation();
        conversation.UpdateStatus(ConversationStatus.Open, TestHelpers.UserId1);
        conversation.ReopenFromClosed(TestHelpers.UserId1);
        Assert.Equal(ConversationStatus.Open, conversation.Status);
    }
}
