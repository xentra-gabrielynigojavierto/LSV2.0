using Comms.Application.Services;
using Comms.Domain.Enums;
using Xunit;

namespace Comms.Tests;

public class UnreadAfterNewMessageTests
{
    [Fact]
    public async Task NewMessage_AfterMarkRead_MakesConversationUnread()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var readStateRepo = TestHelpers.CreateReadStateRepo(db);
        var audit = new NoOpAuditPublisher();

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant1 = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(participant1);

        var participant2 = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId2);
        await participantRepo.AddAsync(participant2);

        var msg1 = TestHelpers.CreateTestMessage(conversation.Id, senderUserId: TestHelpers.UserId2);
        await messageRepo.AddAsync(msg1);

        var readService = new ReadTrackingService(
            readStateRepo, conversationRepo, participantRepo, messageRepo,
            audit, TestHelpers.CreateLogger<ReadTrackingService>());

        await readService.MarkReadAsync(TestHelpers.TenantId, conversation.Id, TestHelpers.UserId1);

        var afterMarkRead = await readService.GetReadStateAsync(
            TestHelpers.TenantId, conversation.Id, TestHelpers.UserId1);
        Assert.False(afterMarkRead.IsUnread);

        await Task.Delay(20);
        var msg2 = TestHelpers.CreateTestMessage(conversation.Id, senderUserId: TestHelpers.UserId2);
        await messageRepo.AddAsync(msg2);

        var afterNewMsg = await readService.GetReadStateAsync(
            TestHelpers.TenantId, conversation.Id, TestHelpers.UserId1);
        Assert.True(afterNewMsg.IsUnread);
    }
}
