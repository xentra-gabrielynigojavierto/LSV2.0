using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Services;
using Comms.Domain.Entities;
using Comms.Domain.Enums;
using Xunit;

namespace Comms.Tests;

public class OutboundEmailTests
{
    private static OutboundEmailService CreateService(
        Infrastructure.Persistence.CommsDbContext db,
        MockNotificationsServiceClient? notifClient = null,
        NoOpAuditPublisher? audit = null)
    {
        return new OutboundEmailService(
            TestHelpers.CreateConversationRepo(db),
            TestHelpers.CreateMessageRepo(db),
            TestHelpers.CreateParticipantRepo(db),
            TestHelpers.CreateAttachmentRepo(db),
            TestHelpers.CreateEmailRefRepo(db),
            TestHelpers.CreateDeliveryStateRepo(db),
            TestHelpers.CreateRecipientRepo(db),
            TestHelpers.CreateSenderConfigRepo(db),
            TestHelpers.CreateTemplateConfigRepo(db),
            notifClient ?? new MockNotificationsServiceClient(),
            TestHelpers.CreateOperationalService(db, audit),
            new NoOpTimelineService(),
            audit ?? new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<OutboundEmailService>());
    }

    private static async Task<(Conversation conv, Message msg, ConversationParticipant participant)> SeedConversationWithMessage(
        Infrastructure.Persistence.CommsDbContext db,
        string visibility = "SharedExternal",
        string channel = "Email",
        Guid? userId = null)
    {
        var convRepo = TestHelpers.CreateConversationRepo(db);
        var msgRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);

        var conv = Conversation.Create(
            TestHelpers.TenantId, TestHelpers.OrgId, "SYNQ_COMMS",
            ContextType.General, $"test-{Guid.NewGuid():N}",
            "Test Outbound Subject", VisibilityType.SharedExternal,
            userId ?? TestHelpers.UserId1);
        await convRepo.AddAsync(conv);

        var effectiveUserId = userId ?? TestHelpers.UserId1;
        var msg = Message.Create(
            conv.Id, TestHelpers.TenantId, TestHelpers.OrgId,
            channel, Direction.Outbound,
            "This is the outbound message body.", visibility,
            effectiveUserId,
            senderUserId: effectiveUserId,
            senderParticipantType: ParticipantType.InternalUser);
        await msgRepo.AddAsync(msg);

        var participant = ConversationParticipant.Create(
            conv.Id, TestHelpers.TenantId, TestHelpers.OrgId,
            ParticipantType.InternalUser, ParticipantRole.Participant,
            canReply: true, createdByUserId: effectiveUserId,
            userId: effectiveUserId);
        await participantRepo.AddAsync(participant);

        return (conv, msg, participant);
    }

    [Fact]
    public async Task AuthorizedInternalParticipant_CanSendOutbound()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);

        var (conv, msg, _) = await SeedConversationWithMessage(db);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.Equal(conv.Id, result.ConversationId);
        Assert.Equal(msg.Id, result.MessageId);
        Assert.NotEqual(Guid.Empty, result.EmailMessageReferenceId);
        Assert.Equal(DeliveryStatus.Queued, result.DeliveryStatus);
        Assert.Contains("@comms.legalsynq.com>", result.GeneratedInternetMessageId);
        Assert.Single(notifClient.SentPayloads);
        Assert.Equal("recipient@example.com", notifClient.SentPayloads[0].ToAddresses);
    }

    [Fact]
    public async Task NonParticipant_CannotSend()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateService(db);

        var (conv, msg, _) = await SeedConversationWithMessage(db);

        var nonParticipantUserId = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SendOutboundAsync(
                new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
                TestHelpers.TenantId, TestHelpers.OrgId, nonParticipantUserId));
    }

    [Fact]
    public async Task InternalOnlyMessage_CannotBeSentOutbound()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateService(db);

        var (conv, _, _) = await SeedConversationWithMessage(db);

        var msgRepo = TestHelpers.CreateMessageRepo(db);
        var internalMsg = Message.Create(
            conv.Id, TestHelpers.TenantId, TestHelpers.OrgId,
            Channel.InApp, Direction.Internal,
            "Internal only message", VisibilityType.InternalOnly,
            TestHelpers.UserId1,
            senderUserId: TestHelpers.UserId1,
            senderParticipantType: ParticipantType.InternalUser);
        await msgRepo.AddAsync(internalMsg);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendOutboundAsync(
                new SendOutboundEmailRequest(conv.Id, internalMsg.Id, "recipient@example.com"),
                TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1));
    }

    [Fact]
    public async Task ReplyThreading_MetadataGeneratedCorrectly()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);
        var emailRefRepo = TestHelpers.CreateEmailRefRepo(db);

        var (conv, msg, _) = await SeedConversationWithMessage(db);

        var priorRef = EmailMessageReference.Create(
            TestHelpers.TenantId, conv.Id, null,
            "<prior@example.com>", EmailDirection.Inbound,
            "sender@example.com", "inbox@tenant.com", "Prior Subject",
            null);
        await emailRefRepo.AddAsync(priorRef);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com",
                ReplyToEmailReferenceId: priorRef.Id),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.Equal(priorRef.Id, result.MatchedReplyReferenceId);

        var sentPayload = notifClient.SentPayloads[0];
        Assert.Equal("<prior@example.com>", sentPayload.InReplyToMessageId);
        Assert.Contains("<prior@example.com>", sentPayload.ReferencesHeader!);
        Assert.Contains("@comms.legalsynq.com>", sentPayload.InternetMessageId);
    }

    [Fact]
    public async Task Attachments_IncludedCorrectly()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);

        var (conv, msg, _) = await SeedConversationWithMessage(db);

        var docId = Guid.NewGuid();
        var attachmentRepo = TestHelpers.CreateAttachmentRepo(db);
        var attachment = MessageAttachment.Create(
            TestHelpers.TenantId, conv.Id, msg.Id,
            docId, "report.pdf", "application/pdf", 4096,
            TestHelpers.UserId1);
        await attachmentRepo.AddAsync(attachment);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com",
                AttachmentDocumentIds: new List<Guid> { docId }),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.Equal(1, result.AttachmentCount);
        Assert.Single(notifClient.SentPayloads[0].Attachments!);
        Assert.Equal("report.pdf", notifClient.SentPayloads[0].Attachments![0].FileName);
    }

    [Fact]
    public async Task DeliveryStatusUpdate_PersistsCorrectly()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);

        var (conv, msg, _) = await SeedConversationWithMessage(db);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        var updateRequest = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: null,
            InternetMessageId: result.GeneratedInternetMessageId,
            Status: "delivered",
            StatusAtUtc: DateTime.UtcNow);

        var updated = await service.ProcessDeliveryStatusAsync(
            updateRequest, TestHelpers.TenantId);

        Assert.True(updated);

        var states = await service.ListDeliveryStatesAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1);
        Assert.Single(states);
        Assert.Equal(DeliveryStatus.Delivered, states[0].DeliveryStatus);
    }

    [Fact]
    public async Task DeliveryStatusUpdate_IdempotentSafe()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);

        var (conv, msg, _) = await SeedConversationWithMessage(db);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        var updateRequest = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: null,
            InternetMessageId: result.GeneratedInternetMessageId,
            Status: "delivered",
            StatusAtUtc: DateTime.UtcNow);

        await service.ProcessDeliveryStatusAsync(updateRequest, TestHelpers.TenantId);
        await service.ProcessDeliveryStatusAsync(updateRequest, TestHelpers.TenantId);

        var states = await service.ListDeliveryStatesAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1);
        Assert.Single(states);
        Assert.Equal(DeliveryStatus.Delivered, states[0].DeliveryStatus);
    }

    [Fact]
    public async Task AuditEvents_EmittedForOutbound()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var audit = new NoOpAuditPublisher();
        var service = CreateService(db, notifClient, audit);

        var (conv, msg, _) = await SeedConversationWithMessage(db);

        await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.Contains(audit.Events, e => e.EventType == "OutboundEmailQueued");

        var updateRequest = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: null,
            InternetMessageId: "some-id",
            Status: "delivered",
            StatusAtUtc: DateTime.UtcNow);

        await service.ProcessDeliveryStatusAsync(updateRequest, TestHelpers.TenantId);
    }

    [Fact]
    public async Task PriorInboundThreading_StillWorks()
    {
        var db = TestHelpers.CreateDbContext();
        var audit = new NoOpAuditPublisher();
        var intakeService = new EmailIntakeService(
            TestHelpers.CreateEmailRefRepo(db),
            TestHelpers.CreateIdentityRepo(db),
            TestHelpers.CreateConversationRepo(db),
            TestHelpers.CreateMessageRepo(db),
            TestHelpers.CreateParticipantRepo(db),
            TestHelpers.CreateAttachmentRepo(db),
            TestHelpers.CreateRecipientRepo(db),
            new MockDocumentServiceClient(),
            TestHelpers.CreateOperationalService(db, audit),
            TestHelpers.CreateQueueRepo(db),
            TestHelpers.CreateAssignmentRepo(db),
            new NoOpTimelineService(),
            audit,
            TestHelpers.CreateLogger<EmailIntakeService>());

        var firstResult = await intakeService.ProcessInboundAsync(
            new InboundEmailIntakeRequest(
                Provider: "test",
                InternetMessageId: "<inbound-first@example.com>",
                ProviderMessageId: null,
                ProviderThreadId: null,
                InReplyToMessageId: null,
                ReferencesHeader: null,
                FromEmail: "external@example.com",
                FromDisplayName: "External User",
                ToAddresses: "inbox@tenant.com",
                CcAddresses: null,
                Subject: "Test inbound thread",
                TextBody: "Hello",
                HtmlBody: null,
                ReceivedAtUtc: DateTime.UtcNow,
                TenantId: TestHelpers.TenantId,
                OrgId: TestHelpers.OrgId));

        Assert.True(firstResult.CreatedNewConversation);

        var replyResult = await intakeService.ProcessInboundAsync(
            new InboundEmailIntakeRequest(
                Provider: "test",
                InternetMessageId: "<inbound-reply@example.com>",
                ProviderMessageId: null,
                ProviderThreadId: null,
                InReplyToMessageId: "<inbound-first@example.com>",
                ReferencesHeader: null,
                FromEmail: "external@example.com",
                FromDisplayName: "External User",
                ToAddresses: "inbox@tenant.com",
                CcAddresses: null,
                Subject: "Re: Test inbound thread",
                TextBody: "Reply",
                HtmlBody: null,
                ReceivedAtUtc: DateTime.UtcNow,
                TenantId: TestHelpers.TenantId,
                OrgId: TestHelpers.OrgId));

        Assert.False(replyResult.CreatedNewConversation);
        Assert.Equal(firstResult.ConversationId, replyResult.ConversationId);
        Assert.Equal(MatchStrategy.InReplyTo, replyResult.MatchedBy);
    }

    [Fact]
    public async Task CanReplyFalse_CannotSend()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateService(db);

        var convRepo = TestHelpers.CreateConversationRepo(db);
        var msgRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);

        var conv = Conversation.Create(
            TestHelpers.TenantId, TestHelpers.OrgId, "SYNQ_COMMS",
            ContextType.General, $"test-{Guid.NewGuid():N}",
            "No Reply Subject", VisibilityType.SharedExternal,
            TestHelpers.UserId2);
        await convRepo.AddAsync(conv);

        var msg = Message.Create(
            conv.Id, TestHelpers.TenantId, TestHelpers.OrgId,
            Channel.Email, Direction.Outbound,
            "Test body", VisibilityType.SharedExternal,
            TestHelpers.UserId2,
            senderUserId: TestHelpers.UserId2,
            senderParticipantType: ParticipantType.InternalUser);
        await msgRepo.AddAsync(msg);

        var participant = ConversationParticipant.Create(
            conv.Id, TestHelpers.TenantId, TestHelpers.OrgId,
            ParticipantType.InternalUser, ParticipantRole.Participant,
            canReply: false, createdByUserId: TestHelpers.UserId2,
            userId: TestHelpers.UserId2);
        await participantRepo.AddAsync(participant);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SendOutboundAsync(
                new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
                TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId2));
    }

    [Fact]
    public async Task UnmatchedDeliveryStatus_ReturnsFalse()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateService(db);

        var updateRequest = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: "nonexistent-provider-id",
            InternetMessageId: "<nonexistent@example.com>",
            Status: "delivered",
            StatusAtUtc: DateTime.UtcNow);

        var result = await service.ProcessDeliveryStatusAsync(
            updateRequest, TestHelpers.TenantId);

        Assert.False(result);
    }

    [Fact]
    public async Task FailedSend_ThrowsAndAllowsRetry()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        notifClient.NextResult = new NotificationsSendResult(
            Success: false,
            NotificationsRequestId: null,
            ProviderUsed: null,
            ProviderMessageId: null,
            Status: "failed",
            ErrorMessage: "Service unavailable");
        var service = CreateService(db, notifClient);

        var (conv, msg, _) = await SeedConversationWithMessage(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendOutboundAsync(
                new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
                TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1));

        notifClient.NextResult = new NotificationsSendResult(
            Success: true,
            NotificationsRequestId: Guid.NewGuid(),
            ProviderUsed: "test-provider",
            ProviderMessageId: null,
            Status: "queued",
            ErrorMessage: null);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.Equal(DeliveryStatus.Queued, result.DeliveryStatus);
    }

    [Fact]
    public async Task TerminalStatus_RejectsRegression()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);

        var (conv, msg, _) = await SeedConversationWithMessage(db);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        var deliveredUpdate = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: null,
            InternetMessageId: result.GeneratedInternetMessageId,
            Status: "delivered",
            StatusAtUtc: DateTime.UtcNow);

        var updated = await service.ProcessDeliveryStatusAsync(deliveredUpdate, TestHelpers.TenantId);
        Assert.True(updated);

        var failedUpdate = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: null,
            InternetMessageId: result.GeneratedInternetMessageId,
            Status: "failed",
            StatusAtUtc: DateTime.UtcNow.AddSeconds(10));

        var regression = await service.ProcessDeliveryStatusAsync(failedUpdate, TestHelpers.TenantId);
        Assert.True(regression);

        var states = await service.ListDeliveryStatesAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1);
        Assert.Single(states);
        Assert.Equal(DeliveryStatus.Delivered, states[0].DeliveryStatus);
    }
}
