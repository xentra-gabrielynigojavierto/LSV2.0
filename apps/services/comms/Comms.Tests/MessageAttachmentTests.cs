using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Services;
using Comms.Domain.Enums;
using Xunit;

namespace Comms.Tests;

public class MessageAttachmentTests
{
    private static readonly Guid ValidDocId = Guid.NewGuid();

    private static MockDocumentServiceClient CreateDocClient(bool exists = true)
    {
        var client = new MockDocumentServiceClient();
        client.SetResult(ValidDocId, new DocumentValidationResult(exists, TestHelpers.TenantId));
        return client;
    }

    private static MessageAttachmentService CreateService(
        Microsoft.EntityFrameworkCore.DbContext _,
        Comms.Infrastructure.Persistence.CommsDbContext db,
        IDocumentServiceClient? docClient = null)
    {
        return new MessageAttachmentService(
            TestHelpers.CreateAttachmentRepo(db),
            TestHelpers.CreateMessageRepo(db),
            TestHelpers.CreateConversationRepo(db),
            TestHelpers.CreateParticipantRepo(db),
            docClient ?? CreateDocClient(),
            new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<MessageAttachmentService>());
    }

    [Fact]
    public async Task LinkAttachment_ValidParticipant_Succeeds()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(participant);

        var msg = TestHelpers.CreateTestMessage(conversation.Id);
        await messageRepo.AddAsync(msg);

        var service = new MessageAttachmentService(
            TestHelpers.CreateAttachmentRepo(db),
            messageRepo, conversationRepo, participantRepo,
            CreateDocClient(),
            new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<MessageAttachmentService>());

        var request = new AddMessageAttachmentRequest(ValidDocId, "test.pdf", "application/pdf", 1024);
        var result = await service.LinkAttachmentAsync(
            TestHelpers.TenantId, TestHelpers.UserId1, conversation.Id, msg.Id, request);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(msg.Id, result.MessageId);
        Assert.Equal(ValidDocId, result.DocumentId);
        Assert.Equal("test.pdf", result.FileName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(1024, result.FileSizeBytes);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task LinkAttachment_NonParticipant_Throws()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var msg = TestHelpers.CreateTestMessage(conversation.Id);
        await messageRepo.AddAsync(msg);

        var service = new MessageAttachmentService(
            TestHelpers.CreateAttachmentRepo(db),
            messageRepo, conversationRepo, participantRepo,
            CreateDocClient(),
            new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<MessageAttachmentService>());

        var request = new AddMessageAttachmentRequest(ValidDocId, "test.pdf", "application/pdf");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LinkAttachmentAsync(
                TestHelpers.TenantId, Guid.NewGuid(), conversation.Id, msg.Id, request));
    }

    [Fact]
    public async Task LinkAttachment_CanReplyFalse_Throws()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant = TestHelpers.CreateTestParticipant(
            conversation.Id, TestHelpers.UserId1, canReply: false);
        await participantRepo.AddAsync(participant);

        var msg = TestHelpers.CreateTestMessage(conversation.Id);
        await messageRepo.AddAsync(msg);

        var service = new MessageAttachmentService(
            TestHelpers.CreateAttachmentRepo(db),
            messageRepo, conversationRepo, participantRepo,
            CreateDocClient(),
            new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<MessageAttachmentService>());

        var request = new AddMessageAttachmentRequest(ValidDocId, "test.pdf", "application/pdf");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LinkAttachmentAsync(
                TestHelpers.TenantId, TestHelpers.UserId1, conversation.Id, msg.Id, request));
    }

    [Fact]
    public async Task LinkAttachment_DocumentNotFound_Throws()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(participant);

        var msg = TestHelpers.CreateTestMessage(conversation.Id);
        await messageRepo.AddAsync(msg);

        var notFoundClient = new MockDocumentServiceClient();
        var service = new MessageAttachmentService(
            TestHelpers.CreateAttachmentRepo(db),
            messageRepo, conversationRepo, participantRepo,
            notFoundClient,
            new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<MessageAttachmentService>());

        var unknownDocId = Guid.NewGuid();
        var request = new AddMessageAttachmentRequest(unknownDocId, "missing.pdf", "application/pdf");
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.LinkAttachmentAsync(
                TestHelpers.TenantId, TestHelpers.UserId1, conversation.Id, msg.Id, request));
    }

    [Fact]
    public async Task LinkAttachment_WrongTenant_Throws()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(participant);

        var msg = TestHelpers.CreateTestMessage(conversation.Id);
        await messageRepo.AddAsync(msg);

        var wrongTenantClient = new MockDocumentServiceClient();
        var wrongTenantDocId = Guid.NewGuid();
        wrongTenantClient.SetResult(wrongTenantDocId,
            new DocumentValidationResult(true, Guid.NewGuid()));

        var service = new MessageAttachmentService(
            TestHelpers.CreateAttachmentRepo(db),
            messageRepo, conversationRepo, participantRepo,
            wrongTenantClient,
            new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<MessageAttachmentService>());

        var request = new AddMessageAttachmentRequest(wrongTenantDocId, "wrong.pdf", "application/pdf");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LinkAttachmentAsync(
                TestHelpers.TenantId, TestHelpers.UserId1, conversation.Id, msg.Id, request));
    }

    [Fact]
    public async Task ExternalUser_CannotSeeInternalOnlyAttachments()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);

        var conversation = TestHelpers.CreateTestConversation(visibility: VisibilityType.SharedExternal);
        await conversationRepo.AddAsync(conversation);

        var internalParticipant = TestHelpers.CreateTestParticipant(
            conversation.Id, TestHelpers.UserId1, ParticipantType.InternalUser);
        await participantRepo.AddAsync(internalParticipant);

        var externalUserId = Guid.NewGuid();
        var externalParticipant = TestHelpers.CreateTestParticipant(
            conversation.Id, externalUserId, ParticipantType.ExternalContact,
            externalName: "External", externalEmail: "ext@test.com");
        await participantRepo.AddAsync(externalParticipant);

        var internalMsg = TestHelpers.CreateTestMessage(conversation.Id, VisibilityType.InternalOnly);
        await messageRepo.AddAsync(internalMsg);

        var attachmentRepo = TestHelpers.CreateAttachmentRepo(db);
        var service = new MessageAttachmentService(
            attachmentRepo, messageRepo, conversationRepo, participantRepo,
            CreateDocClient(),
            new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<MessageAttachmentService>());

        var request = new AddMessageAttachmentRequest(ValidDocId, "internal.pdf", "application/pdf");
        await service.LinkAttachmentAsync(
            TestHelpers.TenantId, TestHelpers.UserId1, conversation.Id, internalMsg.Id, request);

        var externalResult = await service.ListByMessageAsync(
            TestHelpers.TenantId, externalUserId, conversation.Id, internalMsg.Id);
        Assert.Empty(externalResult);

        var internalResult = await service.ListByMessageAsync(
            TestHelpers.TenantId, TestHelpers.UserId1, conversation.Id, internalMsg.Id);
        Assert.Single(internalResult);
    }

    [Fact]
    public async Task LinkAttachment_NullTenantFromDocService_Throws()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(participant);

        var msg = TestHelpers.CreateTestMessage(conversation.Id);
        await messageRepo.AddAsync(msg);

        var nullTenantDocId = Guid.NewGuid();
        var nullTenantClient = new MockDocumentServiceClient();
        nullTenantClient.SetResult(nullTenantDocId, new DocumentValidationResult(true, null));

        var service = new MessageAttachmentService(
            TestHelpers.CreateAttachmentRepo(db),
            messageRepo, conversationRepo, participantRepo,
            nullTenantClient,
            new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<MessageAttachmentService>());

        var request = new AddMessageAttachmentRequest(nullTenantDocId, "unverified.pdf", "application/pdf");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.LinkAttachmentAsync(
                TestHelpers.TenantId, TestHelpers.UserId1, conversation.Id, msg.Id, request));
    }

    [Fact]
    public async Task ExternalUser_CannotRemoveInternalOnlyAttachment()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var attachmentRepo = TestHelpers.CreateAttachmentRepo(db);

        var conversation = TestHelpers.CreateTestConversation(visibility: VisibilityType.SharedExternal);
        await conversationRepo.AddAsync(conversation);

        var internalParticipant = TestHelpers.CreateTestParticipant(
            conversation.Id, TestHelpers.UserId1, ParticipantType.InternalUser);
        await participantRepo.AddAsync(internalParticipant);

        var externalUserId = Guid.NewGuid();
        var externalParticipant = TestHelpers.CreateTestParticipant(
            conversation.Id, externalUserId, ParticipantType.ExternalContact,
            externalName: "External", externalEmail: "ext@test.com");
        await participantRepo.AddAsync(externalParticipant);

        var internalMsg = TestHelpers.CreateTestMessage(conversation.Id, VisibilityType.InternalOnly);
        await messageRepo.AddAsync(internalMsg);

        var service = new MessageAttachmentService(
            attachmentRepo, messageRepo, conversationRepo, participantRepo,
            CreateDocClient(),
            new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<MessageAttachmentService>());

        var request = new AddMessageAttachmentRequest(ValidDocId, "internal.pdf", "application/pdf");
        var linked = await service.LinkAttachmentAsync(
            TestHelpers.TenantId, TestHelpers.UserId1, conversation.Id, internalMsg.Id, request);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RemoveAttachmentAsync(
                TestHelpers.TenantId, externalUserId, conversation.Id, internalMsg.Id, linked.Id));
    }

    [Fact]
    public async Task GetThread_IncludesAttachmentsInMessages()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var readStateRepo = TestHelpers.CreateReadStateRepo(db);
        var attachmentRepo = TestHelpers.CreateAttachmentRepo(db);

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(participant);

        var msg = TestHelpers.CreateTestMessage(conversation.Id);
        await messageRepo.AddAsync(msg);

        var attachmentService = new MessageAttachmentService(
            attachmentRepo, messageRepo, conversationRepo, participantRepo,
            CreateDocClient(),
            new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<MessageAttachmentService>());

        var request = new AddMessageAttachmentRequest(ValidDocId, "thread-doc.pdf", "application/pdf", 4096);
        await attachmentService.LinkAttachmentAsync(
            TestHelpers.TenantId, TestHelpers.UserId1, conversation.Id, msg.Id, request);

        var csAudit = new NoOpAuditPublisher();
        var conversationService = new ConversationService(
            conversationRepo, participantRepo, messageRepo, readStateRepo, attachmentRepo,
            TestHelpers.CreateOperationalService(db, csAudit),
            new NoOpTimelineService(), csAudit,
            TestHelpers.CreateLogger<ConversationService>());

        var thread = await conversationService.GetThreadAsync(
            TestHelpers.TenantId, conversation.Id, TestHelpers.UserId1);

        Assert.Single(thread.Messages);
        Assert.NotNull(thread.Messages[0].Attachments);
        Assert.Single(thread.Messages[0].Attachments!);
        Assert.Equal("thread-doc.pdf", thread.Messages[0].Attachments![0].FileName);
        Assert.Equal(ValidDocId, thread.Messages[0].Attachments![0].DocumentId);
    }

    [Fact]
    public async Task RemoveAttachment_DeactivatesAndPublishesAudit()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var attachmentRepo = TestHelpers.CreateAttachmentRepo(db);

        var conversation = TestHelpers.CreateTestConversation();
        await conversationRepo.AddAsync(conversation);

        var participant = TestHelpers.CreateTestParticipant(conversation.Id, TestHelpers.UserId1);
        await participantRepo.AddAsync(participant);

        var msg = TestHelpers.CreateTestMessage(conversation.Id);
        await messageRepo.AddAsync(msg);

        var service = new MessageAttachmentService(
            attachmentRepo, messageRepo, conversationRepo, participantRepo,
            CreateDocClient(),
            new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<MessageAttachmentService>());

        var request = new AddMessageAttachmentRequest(ValidDocId, "test.pdf", "application/pdf", 2048);
        var linked = await service.LinkAttachmentAsync(
            TestHelpers.TenantId, TestHelpers.UserId1, conversation.Id, msg.Id, request);

        await service.RemoveAttachmentAsync(
            TestHelpers.TenantId, TestHelpers.UserId1, conversation.Id, msg.Id, linked.Id);

        var remaining = await service.ListByMessageAsync(
            TestHelpers.TenantId, TestHelpers.UserId1, conversation.Id, msg.Id);
        Assert.Empty(remaining);
    }
}
