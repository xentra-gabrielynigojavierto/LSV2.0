using Comms.Application.DTOs;
using Comms.Application.Services;
using Comms.Domain.Entities;
using Comms.Domain.Enums;
using Xunit;

namespace Comms.Tests;

public class EmailIntakeTests
{
    private static EmailIntakeService CreateService(
        Infrastructure.Persistence.CommsDbContext db,
        NoOpAuditPublisher? audit = null,
        MockDocumentServiceClient? docClient = null)
    {
        return new EmailIntakeService(
            TestHelpers.CreateEmailRefRepo(db),
            TestHelpers.CreateIdentityRepo(db),
            TestHelpers.CreateConversationRepo(db),
            TestHelpers.CreateMessageRepo(db),
            TestHelpers.CreateParticipantRepo(db),
            TestHelpers.CreateAttachmentRepo(db),
            TestHelpers.CreateRecipientRepo(db),
            docClient ?? new MockDocumentServiceClient(),
            TestHelpers.CreateOperationalService(db, audit),
            TestHelpers.CreateQueueRepo(db),
            TestHelpers.CreateAssignmentRepo(db),
            new NoOpTimelineService(),
            audit ?? new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<EmailIntakeService>());
    }

    private static InboundEmailIntakeRequest CreateIntakeRequest(
        string? internetMessageId = null,
        string? inReplyTo = null,
        string? referencesHeader = null,
        string? providerThreadId = null,
        string? subject = null,
        string fromEmail = "sender@example.com",
        string? fromDisplayName = null,
        List<EmailAttachmentDescriptor>? attachments = null)
    {
        return new InboundEmailIntakeRequest(
            Provider: "test",
            InternetMessageId: internetMessageId ?? $"<{Guid.NewGuid()}@example.com>",
            ProviderMessageId: null,
            ProviderThreadId: providerThreadId,
            InReplyToMessageId: inReplyTo,
            ReferencesHeader: referencesHeader,
            FromEmail: fromEmail,
            FromDisplayName: fromDisplayName,
            ToAddresses: "inbox@tenant.com",
            CcAddresses: null,
            Subject: subject ?? "Test Email Subject",
            TextBody: "Hello, this is a test email.",
            HtmlBody: null,
            ReceivedAtUtc: DateTime.UtcNow,
            TenantId: TestHelpers.TenantId,
            OrgId: TestHelpers.OrgId,
            Attachments: attachments);
    }

    [Fact]
    public async Task InReplyTo_MatchesExistingConversation()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateService(db);

        var firstResult = await service.ProcessInboundAsync(CreateIntakeRequest(
            internetMessageId: "<first@example.com>",
            subject: "Initial email"));

        Assert.True(firstResult.CreatedNewConversation);

        var replyResult = await service.ProcessInboundAsync(CreateIntakeRequest(
            internetMessageId: "<reply@example.com>",
            inReplyTo: "<first@example.com>",
            subject: "Re: Initial email"));

        Assert.False(replyResult.CreatedNewConversation);
        Assert.Equal(firstResult.ConversationId, replyResult.ConversationId);
        Assert.Equal(MatchStrategy.InReplyTo, replyResult.MatchedBy);
    }

    [Fact]
    public async Task NewConversation_CreatedWhenNoMatch()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateService(db);

        var result = await service.ProcessInboundAsync(CreateIntakeRequest(
            subject: "Brand new topic"));

        Assert.True(result.CreatedNewConversation);
        Assert.Equal(MatchStrategy.NewConversation, result.MatchedBy);
        Assert.NotEqual(Guid.Empty, result.ConversationId);
        Assert.NotEqual(Guid.Empty, result.LinkedMessageId);
        Assert.NotEqual(Guid.Empty, result.EmailReferenceId);
    }

    [Fact]
    public async Task ConversationToken_MatchesCorrectly()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateService(db);

        var firstResult = await service.ProcessInboundAsync(CreateIntakeRequest(
            subject: "Setup conversation"));
        Assert.True(firstResult.CreatedNewConversation);

        var token = $"[COMMS-{firstResult.ConversationId}]";
        var tokenResult = await service.ProcessInboundAsync(CreateIntakeRequest(
            internetMessageId: "<token-match@example.com>",
            subject: $"Re: Setup conversation {token}"));

        Assert.False(tokenResult.CreatedNewConversation);
        Assert.Equal(firstResult.ConversationId, tokenResult.ConversationId);
        Assert.Equal(MatchStrategy.ConversationToken, tokenResult.MatchedBy);
    }

    [Fact]
    public async Task ExternalIdentity_ReusedNotDuplicated()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateService(db);

        var result1 = await service.ProcessInboundAsync(CreateIntakeRequest(
            internetMessageId: "<first-from-alice@example.com>",
            fromEmail: "Alice@Example.COM",
            fromDisplayName: "Alice Smith"));
        Assert.True(result1.CreatedNewParticipant);

        var result2 = await service.ProcessInboundAsync(CreateIntakeRequest(
            internetMessageId: "<second-from-alice@example.com>",
            inReplyTo: "<first-from-alice@example.com>",
            fromEmail: " alice@example.com ",
            fromDisplayName: "Alice Smith"));
        Assert.False(result2.CreatedNewParticipant);
        Assert.Equal(result1.ConversationId, result2.ConversationId);
    }

    [Fact]
    public async Task NoUnsafeHeuristicMatch_CreatesNewConversation()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateService(db);

        var result1 = await service.ProcessInboundAsync(CreateIntakeRequest(
            internetMessageId: "<msg-a@example.com>",
            subject: "Common Subject",
            fromEmail: "user@example.com"));
        Assert.True(result1.CreatedNewConversation);

        var result2 = await service.ProcessInboundAsync(CreateIntakeRequest(
            internetMessageId: "<msg-b@example.com>",
            subject: "Common Subject",
            fromEmail: "user@example.com"));
        Assert.True(result2.CreatedNewConversation);
        Assert.NotEqual(result1.ConversationId, result2.ConversationId);
    }

    [Fact]
    public async Task EmailReference_PersistedCorrectly()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateService(db);

        var internetMsgId = "<persist-test@example.com>";
        var result = await service.ProcessInboundAsync(CreateIntakeRequest(
            internetMessageId: internetMsgId,
            subject: "Persistence test",
            fromEmail: "persister@example.com"));

        var systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var systemParticipant = TestHelpers.CreateTestParticipant(
            result.ConversationId, systemUserId, "InternalUser");
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        await participantRepo.AddAsync(systemParticipant);

        var refs = await service.ListEmailReferencesAsync(
            TestHelpers.TenantId, result.ConversationId, systemUserId);

        Assert.Single(refs);
        Assert.Equal(internetMsgId, refs[0].InternetMessageId);
        Assert.Equal("persister@example.com", refs[0].FromEmail);
        Assert.Equal(EmailDirection.Inbound, refs[0].EmailDirection);
        Assert.Equal(result.ConversationId, refs[0].ConversationId);
        Assert.Equal(result.LinkedMessageId, refs[0].MessageId);
    }

    [Fact]
    public async Task InboundAttachment_LinkedCorrectly()
    {
        var db = TestHelpers.CreateDbContext();
        var docClient = new MockDocumentServiceClient();
        var docId = Guid.NewGuid();
        var invalidDocId = Guid.NewGuid();
        docClient.SetResult(docId, new Comms.Application.Interfaces.DocumentValidationResult(true, TestHelpers.TenantId));
        docClient.SetResult(invalidDocId, new Comms.Application.Interfaces.DocumentValidationResult(false, null));

        var service = CreateService(db, docClient: docClient);

        var attachments = new List<EmailAttachmentDescriptor>
        {
            new(docId, "invoice.pdf", "application/pdf", 2048),
            new(invalidDocId, "bad-doc.pdf", "application/pdf", 1024),
            new(null, "unknown.txt", "text/plain", 100)
        };

        var result = await service.ProcessInboundAsync(CreateIntakeRequest(
            subject: "Email with attachment",
            attachments: attachments));

        Assert.Equal(1, result.AttachmentCountProcessed);
    }

    [Fact]
    public async Task AuditEvents_EmittedForIntake()
    {
        var db = TestHelpers.CreateDbContext();
        var audit = new NoOpAuditPublisher();
        var service = CreateService(db, audit);

        await service.ProcessInboundAsync(CreateIntakeRequest(
            subject: "Audit test email"));

        Assert.Contains(audit.Events, e => e.EventType == "InboundEmailReceived");
        Assert.Contains(audit.Events, e => e.EventType == "InboundEmailNewConversation");
        Assert.Contains(audit.Events, e => e.EventType == "ExternalParticipantCreated");
    }

    [Fact]
    public async Task DuplicateEmail_IgnoredGracefully()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateService(db);

        var internetMsgId = "<duplicate@example.com>";
        var result1 = await service.ProcessInboundAsync(CreateIntakeRequest(
            internetMessageId: internetMsgId));
        Assert.True(result1.CreatedNewConversation);

        var result2 = await service.ProcessInboundAsync(CreateIntakeRequest(
            internetMessageId: internetMsgId));
        Assert.False(result2.CreatedNewConversation);
        Assert.Equal("Duplicate", result2.MatchedBy);
        Assert.Equal(result1.ConversationId, result2.ConversationId);
    }

    [Fact]
    public async Task ReferencesHeader_MatchesConversation()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateService(db);

        var result1 = await service.ProcessInboundAsync(CreateIntakeRequest(
            internetMessageId: "<original@example.com>"));
        Assert.True(result1.CreatedNewConversation);

        var result2 = await service.ProcessInboundAsync(CreateIntakeRequest(
            internetMessageId: "<chain@example.com>",
            referencesHeader: "<original@example.com> <other@example.com>"));

        Assert.False(result2.CreatedNewConversation);
        Assert.Equal(result1.ConversationId, result2.ConversationId);
        Assert.Equal(MatchStrategy.References, result2.MatchedBy);
    }

    [Fact]
    public async Task ProviderThread_MatchesConversation()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateService(db);

        var threadId = "provider-thread-abc123";
        var result1 = await service.ProcessInboundAsync(CreateIntakeRequest(
            internetMessageId: "<thread-orig@example.com>",
            providerThreadId: threadId));
        Assert.True(result1.CreatedNewConversation);

        var result2 = await service.ProcessInboundAsync(CreateIntakeRequest(
            internetMessageId: "<thread-reply@example.com>",
            providerThreadId: threadId));

        Assert.False(result2.CreatedNewConversation);
        Assert.Equal(result1.ConversationId, result2.ConversationId);
        Assert.Equal(MatchStrategy.ProviderThread, result2.MatchedBy);
    }

    [Fact]
    public async Task PriorBLK_VisibilityStillWorks()
    {
        var db = TestHelpers.CreateDbContext();
        var conversationRepo = TestHelpers.CreateConversationRepo(db);
        var messageRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var audit = new NoOpAuditPublisher();

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
        var sharedMsg = TestHelpers.CreateTestMessage(conversation.Id, VisibilityType.SharedExternal);
        await messageRepo.AddAsync(internalMsg);
        await messageRepo.AddAsync(sharedMsg);

        var msgService = new MessageService(
            messageRepo, conversationRepo, participantRepo,
            new NoOpTimelineService(), new NoOpMentionService(), audit, TestHelpers.CreateLogger<MessageService>());

        var externalMessages = await msgService.ListByConversationAsync(
            TestHelpers.TenantId, conversation.Id, externalUserId);
        Assert.Single(externalMessages);
        Assert.Equal(VisibilityType.SharedExternal, externalMessages[0].VisibilityType);

        var internalMessages = await msgService.ListByConversationAsync(
            TestHelpers.TenantId, conversation.Id, TestHelpers.UserId1);
        Assert.Equal(2, internalMessages.Count);
    }
}
