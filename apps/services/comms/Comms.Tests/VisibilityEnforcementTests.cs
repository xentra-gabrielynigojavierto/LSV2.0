using Comms.Application.DTOs;
using Comms.Application.Services;
using Comms.Domain.Enums;
using Xunit;

namespace Comms.Tests;

public class VisibilityEnforcementTests
{
    [Fact]
    public async Task InternalOnlyMessages_NotVisibleToExternalParticipants()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var readStateRepo = TestHelpers.CreateReadStateRepo(db);
        var audit = new NoOpAuditPublisher();

        var conversation = TestHelpers.CreateTestConversation(visibility: VisibilityType.SharedExternal);
        await conversationRepo.AddAsync(conversation);

        var internalParticipant = TestHelpers.CreateTestParticipant(
            conversation.Id, TestHelpers.UserId1, ParticipantType.InternalUser);
        await participantRepo.AddAsync(internalParticipant);

        var externalUserId = Guid.NewGuid();
        var externalParticipant = TestHelpers.CreateTestParticipant(
            conversation.Id, externalUserId, ParticipantType.ExternalContact,
            externalName: "External User", externalEmail: "ext@test.com");
        await participantRepo.AddAsync(externalParticipant);

        var internalMsg = TestHelpers.CreateTestMessage(conversation.Id, VisibilityType.InternalOnly);
        var sharedMsg = TestHelpers.CreateTestMessage(conversation.Id, VisibilityType.SharedExternal);
        await messageRepo.AddAsync(internalMsg);
        await messageRepo.AddAsync(sharedMsg);

        var service = new MessageService(
            messageRepo, conversationRepo, participantRepo,
            new NoOpTimelineService(), new NoOpMentionService(), audit, TestHelpers.CreateLogger<MessageService>());

        var externalMessages = await service.ListByConversationAsync(
            TestHelpers.TenantId, conversation.Id, externalUserId);

        Assert.Single(externalMessages);
        Assert.Equal(VisibilityType.SharedExternal, externalMessages[0].VisibilityType);

        var internalMessages = await service.ListByConversationAsync(
            TestHelpers.TenantId, conversation.Id, TestHelpers.UserId1);

        Assert.Equal(2, internalMessages.Count);
    }

    [Fact]
    public void SystemNote_CannotBeSharedExternal()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TestHelpers.CreateTestMessage(
                Guid.NewGuid(),
                visibility: VisibilityType.SharedExternal,
                channel: Channel.SystemNote));
    }
}
