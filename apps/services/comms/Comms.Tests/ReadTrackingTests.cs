using Comms.Application.Services;
using Comms.Domain.Enums;
using Xunit;

namespace Comms.Tests;

public class ReadTrackingTests
{
    [Fact]
    public async Task MarkRead_PersistsReadState_AndShowsNotUnread()
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

        var msg = TestHelpers.CreateTestMessage(conversation.Id);
        await messageRepo.AddAsync(msg);

        var readService = new ReadTrackingService(
            readStateRepo, conversationRepo, participantRepo, messageRepo,
            audit, TestHelpers.CreateLogger<ReadTrackingService>());

        var beforeRead = await readService.GetReadStateAsync(
            TestHelpers.TenantId, conversation.Id, TestHelpers.UserId1);
        Assert.True(beforeRead.IsUnread);

        var result = await readService.MarkReadAsync(
            TestHelpers.TenantId, conversation.Id, TestHelpers.UserId1);
        Assert.False(result.IsUnread);
        Assert.NotNull(result.LastReadAtUtc);
        Assert.Equal(msg.Id, result.LastReadMessageId);

        var afterRead = await readService.GetReadStateAsync(
            TestHelpers.TenantId, conversation.Id, TestHelpers.UserId1);
        Assert.False(afterRead.IsUnread);
    }
}
