using Comms.Application.Services;
using Comms.Domain.Enums;
using Xunit;

namespace Comms.Tests;

public class OrderedThreadRetrievalTests
{
    [Fact]
    public async Task GetThread_ReturnsMessages_InDeterministicOrder()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var readStateRepo = TestHelpers.CreateReadStateRepo(db);
        var audit = new NoOpAuditPublisher();

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(participant);

        var msg1 = TestHelpers.CreateTestMessage(conversation.Id);
        await Task.Delay(10);
        var msg2 = TestHelpers.CreateTestMessage(conversation.Id);
        await Task.Delay(10);
        var msg3 = TestHelpers.CreateTestMessage(conversation.Id);

        await messageRepo.AddAsync(msg1);
        await messageRepo.AddAsync(msg2);
        await messageRepo.AddAsync(msg3);

        var attachmentRepo = TestHelpers.CreateAttachmentRepo(db);
        var service = new ConversationService(
            conversationRepo, participantRepo, messageRepo, readStateRepo, attachmentRepo,
            TestHelpers.CreateOperationalService(db, audit),
            new NoOpTimelineService(), audit, TestHelpers.CreateLogger<ConversationService>());

        var thread = await service.GetThreadAsync(
            TestHelpers.TenantId, conversation.Id, TestHelpers.UserId1);

        Assert.Equal(3, thread.Messages.Count);
        Assert.True(thread.Messages[0].SentAtUtc <= thread.Messages[1].SentAtUtc);
        Assert.True(thread.Messages[1].SentAtUtc <= thread.Messages[2].SentAtUtc);
        Assert.Equal(msg1.Id, thread.Messages[0].Id);
        Assert.Equal(msg2.Id, thread.Messages[1].Id);
        Assert.Equal(msg3.Id, thread.Messages[2].Id);
    }
}
