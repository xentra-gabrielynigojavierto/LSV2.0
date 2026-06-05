using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Services;
using Comms.Domain.Entities;
using Comms.Domain.Enums;
using Xunit;

namespace Comms.Tests;

public class E2ENotificationsIntegrationTests
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

    private static async Task<(Conversation conv, Message msg, ConversationParticipant participant)> SeedConversation(
        Infrastructure.Persistence.CommsDbContext db)
    {
        var convRepo = TestHelpers.CreateConversationRepo(db);
        var msgRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);

        var conv = Conversation.Create(
            TestHelpers.TenantId, TestHelpers.OrgId, "SYNQ_COMMS",
            ContextType.General, $"e2e-test-{Guid.NewGuid():N}",
            "E2E Test Subject", VisibilityType.SharedExternal,
            TestHelpers.UserId1);
        await convRepo.AddAsync(conv);

        var msg = Message.Create(
            conv.Id, TestHelpers.TenantId, TestHelpers.OrgId,
            Channel.Email, Direction.Outbound,
            "E2E test message body.", VisibilityType.SharedExternal,
            TestHelpers.UserId1,
            senderUserId: TestHelpers.UserId1,
            senderParticipantType: ParticipantType.InternalUser);
        await msgRepo.AddAsync(msg);

        var participant = ConversationParticipant.Create(
            conv.Id, TestHelpers.TenantId, TestHelpers.OrgId,
            ParticipantType.InternalUser, ParticipantRole.Participant,
            canReply: true, createdByUserId: TestHelpers.UserId1,
            userId: TestHelpers.UserId1);
        await participantRepo.AddAsync(participant);

        return (conv, msg, participant);
    }

    [Fact]
    public async Task Test1_OutboundPayloadContract_SenderTemplateThreadingRecipientAttachment()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);

        var senderConfigRepo = TestHelpers.CreateSenderConfigRepo(db);
        var senderConfig = TenantEmailSenderConfig.Create(
            TestHelpers.TenantId, "Legal Team", "legal@tenant.com",
            SenderType.Support, TestHelpers.UserId1,
            isDefault: true,
            verificationStatus: VerificationStatus.Verified,
            replyToEmail: "reply@tenant.com");
        await senderConfigRepo.AddAsync(senderConfig);
        await senderConfigRepo.SaveChangesAsync();

        var templateRepo = TestHelpers.CreateTemplateConfigRepo(db);
        var template = EmailTemplateConfig.Create(
            "e2e_template", "E2E Template", "TENANT", TestHelpers.UserId1,
            tenantId: TestHelpers.TenantId,
            subjectTemplate: "Legal: {{topic}}",
            bodyTextTemplate: "Dear {{name}}, {{content}}",
            bodyHtmlTemplate: "<p>Dear {{name}}, {{content}}</p>");
        await templateRepo.AddAsync(template);
        await templateRepo.SaveChangesAsync();

        var docId = Guid.NewGuid();
        var attachmentRepo = TestHelpers.CreateAttachmentRepo(db);
        var att = MessageAttachment.Create(
            TestHelpers.TenantId, conv.Id, msg.Id,
            docId, "contract.pdf", "application/pdf", 8192,
            TestHelpers.UserId1);
        await attachmentRepo.AddAsync(att);

        var emailRefRepo = TestHelpers.CreateEmailRefRepo(db);
        var priorRef = EmailMessageReference.Create(
            TestHelpers.TenantId, conv.Id, null,
            "<prior-e2e@example.com>", EmailDirection.Inbound,
            "external@example.com", "inbox@tenant.com", "Prior Subject", null);
        await emailRefRepo.AddAsync(priorRef);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com",
                CcAddresses: "cc@example.com",
                BccAddresses: "bcc@example.com",
                TemplateKey: "e2e_template",
                TemplateVariables: new Dictionary<string, string> { ["topic"] = "Contract Review", ["name"] = "Client", ["content"] = "please review" },
                ReplyToEmailReferenceId: priorRef.Id,
                AttachmentDocumentIds: new List<Guid> { docId }),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.Single(notifClient.SentPayloads);
        var payload = notifClient.SentPayloads[0];

        Assert.Equal("legal@tenant.com", payload.FromEmail);
        Assert.Equal("Legal Team", payload.FromDisplayName);
        Assert.Equal("reply@tenant.com", payload.ReplyToEmail);

        Assert.Equal("Legal: Contract Review", payload.Subject);
        Assert.Contains("Dear Client", payload.BodyText!);
        Assert.Contains("<p>Dear Client", payload.BodyHtml!);

        Assert.Equal("recipient@example.com", payload.ToAddresses);
        Assert.Equal("cc@example.com", payload.CcAddresses);
        Assert.Equal("bcc@example.com", payload.BccAddresses);

        Assert.NotNull(payload.Attachments);
        Assert.Single(payload.Attachments);
        Assert.Equal("contract.pdf", payload.Attachments[0].FileName);
        Assert.Equal(docId, payload.Attachments[0].DocumentId);

        Assert.Contains("@comms.legalsynq.com>", payload.InternetMessageId);
        Assert.Equal("<prior-e2e@example.com>", payload.InReplyToMessageId);
        Assert.Contains("<prior-e2e@example.com>", payload.ReferencesHeader!);

        Assert.StartsWith("comms-outbound-", payload.IdempotencyKey);
        Assert.Equal("e2e_template", payload.TemplateKey);

        Assert.Equal("TEMPLATE", result.CompositionMode);
        Assert.Equal("legal@tenant.com", result.SenderEmail);
        Assert.NotNull(result.SenderConfigId);
    }

    [Fact]
    public async Task Test2_SuccessfulDeliveryLifecycle_QueuedSentDelivered()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.Equal(DeliveryStatus.Queued, result.DeliveryStatus);

        var baseTime = DateTime.UtcNow;

        var sentUpdate = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: "sg-msg-001",
            InternetMessageId: result.GeneratedInternetMessageId,
            Status: "sent",
            StatusAtUtc: baseTime.AddSeconds(5));
        var sentResult = await service.ProcessDeliveryStatusAsync(sentUpdate, TestHelpers.TenantId);
        Assert.True(sentResult);

        var states1 = await service.ListDeliveryStatesAsync(TestHelpers.TenantId, conv.Id, TestHelpers.UserId1);
        Assert.Single(states1);
        Assert.Equal(DeliveryStatus.Sent, states1[0].DeliveryStatus);

        var deliveredUpdate = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: "sg-msg-001",
            InternetMessageId: result.GeneratedInternetMessageId,
            Status: "delivered",
            StatusAtUtc: baseTime.AddSeconds(10));
        var deliveredResult = await service.ProcessDeliveryStatusAsync(deliveredUpdate, TestHelpers.TenantId);
        Assert.True(deliveredResult);

        var states2 = await service.ListDeliveryStatesAsync(TestHelpers.TenantId, conv.Id, TestHelpers.UserId1);
        Assert.Single(states2);
        Assert.Equal(DeliveryStatus.Delivered, states2[0].DeliveryStatus);
    }

    [Fact]
    public async Task Test3_FailedDeliveryLifecycle_QueuedToBounced()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        var bouncedUpdate = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: "sg-msg-bounced",
            InternetMessageId: result.GeneratedInternetMessageId,
            Status: "bounced",
            StatusAtUtc: DateTime.UtcNow.AddSeconds(5),
            ErrorCode: "550",
            ErrorMessage: "Mailbox not found");
        var bouncedResult = await service.ProcessDeliveryStatusAsync(bouncedUpdate, TestHelpers.TenantId);
        Assert.True(bouncedResult);

        var states = await service.ListDeliveryStatesAsync(TestHelpers.TenantId, conv.Id, TestHelpers.UserId1);
        Assert.Single(states);
        Assert.Equal(DeliveryStatus.Bounced, states[0].DeliveryStatus);
        Assert.Equal("550", states[0].LastErrorCode);
        Assert.Equal("Mailbox not found", states[0].LastErrorMessage);
    }

    [Fact]
    public async Task Test4_DuplicateCallbackIdempotency_NoCorruptState()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var audit = new NoOpAuditPublisher();
        var service = CreateService(db, notifClient, audit);
        var (conv, msg, _) = await SeedConversation(db);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        var deliveredUpdate = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: "sg-msg-dup",
            InternetMessageId: result.GeneratedInternetMessageId,
            Status: "delivered",
            StatusAtUtc: DateTime.UtcNow.AddSeconds(5));

        var first = await service.ProcessDeliveryStatusAsync(deliveredUpdate, TestHelpers.TenantId);
        Assert.True(first);

        var second = await service.ProcessDeliveryStatusAsync(deliveredUpdate, TestHelpers.TenantId);
        Assert.True(second);

        var states = await service.ListDeliveryStatesAsync(TestHelpers.TenantId, conv.Id, TestHelpers.UserId1);
        Assert.Single(states);
        Assert.Equal(DeliveryStatus.Delivered, states[0].DeliveryStatus);

        Assert.Contains(audit.Events, e => e.EventType == "DeliveryCallbackIgnored");
    }

    [Fact]
    public async Task Test5_OutOfOrderCallbackSafety_DeliveredBeforeSentAndTerminalProtection()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        var baseTime = DateTime.UtcNow;

        var deliveredFirst = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: null,
            InternetMessageId: result.GeneratedInternetMessageId,
            Status: "delivered",
            StatusAtUtc: baseTime.AddSeconds(10));
        var r1 = await service.ProcessDeliveryStatusAsync(deliveredFirst, TestHelpers.TenantId);
        Assert.True(r1);

        var sentLate = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: null,
            InternetMessageId: result.GeneratedInternetMessageId,
            Status: "sent",
            StatusAtUtc: baseTime.AddSeconds(5));
        var r2 = await service.ProcessDeliveryStatusAsync(sentLate, TestHelpers.TenantId);
        Assert.True(r2);

        var failedAfterTerminal = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: null,
            InternetMessageId: result.GeneratedInternetMessageId,
            Status: "failed",
            StatusAtUtc: baseTime.AddSeconds(20));
        var r3 = await service.ProcessDeliveryStatusAsync(failedAfterTerminal, TestHelpers.TenantId);
        Assert.True(r3);

        var states = await service.ListDeliveryStatesAsync(TestHelpers.TenantId, conv.Id, TestHelpers.UserId1);
        Assert.Single(states);
        Assert.Equal(DeliveryStatus.Delivered, states[0].DeliveryStatus);
    }

    [Fact]
    public async Task Test6_UnmatchedCallbackHandling_ReturnsFalseAndAudits()
    {
        var db = TestHelpers.CreateDbContext();
        var audit = new NoOpAuditPublisher();
        var service = CreateService(db, audit: audit);

        var updateRequest = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: "unknown-provider-id",
            InternetMessageId: "<unknown@example.com>",
            Status: "delivered",
            StatusAtUtc: DateTime.UtcNow,
            NotificationsRequestId: Guid.NewGuid().ToString());

        var result = await service.ProcessDeliveryStatusAsync(updateRequest, TestHelpers.TenantId);

        Assert.False(result);
        Assert.Contains(audit.Events, e => e.EventType == "DeliveryCallbackUnmatched");
    }

    [Fact]
    public async Task Test7_NotificationsUnavailable_SendFailsSafely()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var audit = new NoOpAuditPublisher();
        notifClient.NextResult = new NotificationsSendResult(
            Success: false,
            NotificationsRequestId: null,
            ProviderUsed: null,
            ProviderMessageId: null,
            Status: "failed",
            ErrorMessage: "Connection refused: Notifications service unavailable");
        var service = CreateService(db, notifClient, audit);

        var (conv, msg, _) = await SeedConversation(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendOutboundAsync(
                new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
                TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1));

        Assert.Contains("Notifications service", ex.Message);
        Assert.Contains(audit.Events, e => e.EventType == "OutboundEmailFailed");

        var deliveryRepo = TestHelpers.CreateDeliveryStateRepo(db);
        var emailRefRepo = TestHelpers.CreateEmailRefRepo(db);
        var states = await deliveryRepo.ListByConversationAsync(TestHelpers.TenantId, conv.Id);
        var refs = await emailRefRepo.FindByMessageIdAsync(TestHelpers.TenantId, msg.Id);
        Assert.Empty(states);
        Assert.Null(refs);
    }

    [Fact]
    public async Task Test8_DeliveryCallbackProcessing_WithNotificationsRequestId_Succeeds()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        var notificationsRequestId = result.NotificationsRequestId?.ToString();

        var callbackRequest = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: null,
            InternetMessageId: result.GeneratedInternetMessageId,
            Status: "delivered",
            StatusAtUtc: DateTime.UtcNow.AddSeconds(5),
            NotificationsRequestId: notificationsRequestId);

        var matched = await service.ProcessDeliveryStatusAsync(callbackRequest, TestHelpers.TenantId);
        Assert.True(matched);

        var states = await service.ListDeliveryStatesAsync(TestHelpers.TenantId, conv.Id, TestHelpers.UserId1);
        Assert.Single(states);
        Assert.Equal(DeliveryStatus.Delivered, states[0].DeliveryStatus);
    }

    [Fact]
    public async Task Test9_SenderTemplateAttachmentPropagation_FullPipeline()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);

        var senderConfigRepo = TestHelpers.CreateSenderConfigRepo(db);
        var sender = TenantEmailSenderConfig.Create(
            TestHelpers.TenantId, "Support Desk", "support@tenant.com",
            SenderType.Support, TestHelpers.UserId1,
            isDefault: true,
            verificationStatus: VerificationStatus.Verified);
        await senderConfigRepo.AddAsync(sender);
        await senderConfigRepo.SaveChangesAsync();

        var templateRepo = TestHelpers.CreateTemplateConfigRepo(db);
        var tmpl = EmailTemplateConfig.Create(
            "propagation_test", "Propagation Template", "TENANT", TestHelpers.UserId1,
            tenantId: TestHelpers.TenantId,
            subjectTemplate: "Support: {{subject}}",
            bodyTextTemplate: "Hello {{name}}");
        await templateRepo.AddAsync(tmpl);
        await templateRepo.SaveChangesAsync();

        var docId = Guid.NewGuid();
        var attachRepo = TestHelpers.CreateAttachmentRepo(db);
        var att = MessageAttachment.Create(
            TestHelpers.TenantId, conv.Id, msg.Id,
            docId, "evidence.png", "image/png", 2048,
            TestHelpers.UserId1);
        await attachRepo.AddAsync(att);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "external@example.com",
                TemplateKey: "propagation_test",
                TemplateVariables: new Dictionary<string, string> { ["subject"] = "Case Update", ["name"] = "Client" },
                AttachmentDocumentIds: new List<Guid> { docId }),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        var payload = notifClient.SentPayloads[0];

        Assert.Equal("support@tenant.com", payload.FromEmail);
        Assert.Equal("Support Desk", payload.FromDisplayName);
        Assert.Equal("Support: Case Update", payload.Subject);
        Assert.Contains("Hello Client", payload.BodyText!);
        Assert.NotNull(payload.Attachments);
        Assert.Equal("evidence.png", payload.Attachments[0].FileName);
        Assert.Equal(docId, payload.Attachments[0].DocumentId);

        Assert.Equal(sender.Id, result.SenderConfigId);
        Assert.Equal("propagation_test", result.TemplateKey);
        Assert.Equal(tmpl.Id, result.TemplateConfigId);
        Assert.Equal("TEMPLATE", result.CompositionMode);
    }

    [Fact]
    public async Task Test10_BccConfidentiality_PresentInPayloadAbsentFromVisibleAPIs()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "to@example.com",
                CcAddresses: "cc@example.com",
                BccAddresses: "bcc-secret@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        var payload = notifClient.SentPayloads[0];
        Assert.Equal("bcc-secret@example.com", payload.BccAddresses);

        var recipientRepo = TestHelpers.CreateRecipientRepo(db);
        var visibleRecipients = await recipientRepo.ListVisibleByEmailReferenceAsync(
            TestHelpers.TenantId, result.EmailMessageReferenceId);

        Assert.DoesNotContain(visibleRecipients,
            r => r.NormalizedEmail.Contains("bcc-secret", StringComparison.OrdinalIgnoreCase));

        var toRecipients = visibleRecipients.Where(r => r.RecipientType == RecipientType.To).ToList();
        var ccRecipients = visibleRecipients.Where(r => r.RecipientType == RecipientType.Cc).ToList();
        Assert.Single(toRecipients);
        Assert.Single(ccRecipients);
        Assert.Contains("to@example.com", toRecipients[0].NormalizedEmail);
        Assert.Contains("cc@example.com", ccRecipients[0].NormalizedEmail);
    }

    [Fact]
    public async Task Test11_PriorThreadingRegression_InboundOutboundThreadingPreserved()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);

        var e2eAudit = new NoOpAuditPublisher();
        var intakeService = new EmailIntakeService(
            TestHelpers.CreateEmailRefRepo(db),
            TestHelpers.CreateIdentityRepo(db),
            TestHelpers.CreateConversationRepo(db),
            TestHelpers.CreateMessageRepo(db),
            TestHelpers.CreateParticipantRepo(db),
            TestHelpers.CreateAttachmentRepo(db),
            TestHelpers.CreateRecipientRepo(db),
            new MockDocumentServiceClient(),
            TestHelpers.CreateOperationalService(db, e2eAudit),
            TestHelpers.CreateQueueRepo(db),
            TestHelpers.CreateAssignmentRepo(db),
            new NoOpTimelineService(),
            e2eAudit,
            TestHelpers.CreateLogger<EmailIntakeService>());

        var inboundResult = await intakeService.ProcessInboundAsync(
            new InboundEmailIntakeRequest(
                Provider: "test",
                InternetMessageId: "<e2e-inbound@example.com>",
                ProviderMessageId: null,
                ProviderThreadId: null,
                InReplyToMessageId: null,
                ReferencesHeader: null,
                FromEmail: "external@example.com",
                FromDisplayName: "External User",
                ToAddresses: "inbox@tenant.com",
                CcAddresses: null,
                Subject: "E2E Thread Test",
                TextBody: "Hello from outside",
                HtmlBody: null,
                ReceivedAtUtc: DateTime.UtcNow,
                TenantId: TestHelpers.TenantId,
                OrgId: TestHelpers.OrgId));

        Assert.True(inboundResult.CreatedNewConversation);
        var convId = inboundResult.ConversationId;

        var convRepo = TestHelpers.CreateConversationRepo(db);
        var conv = await convRepo.GetByIdAsync(TestHelpers.TenantId, convId);
        Assert.NotNull(conv);

        var participantRepo = TestHelpers.CreateParticipantRepo(db);
        var existingParticipant = await participantRepo.GetActiveByUserIdAsync(
            TestHelpers.TenantId, convId, TestHelpers.UserId1);
        if (existingParticipant is null)
        {
            var participant = ConversationParticipant.Create(
                convId, TestHelpers.TenantId, TestHelpers.OrgId,
                ParticipantType.InternalUser, ParticipantRole.Participant,
                canReply: true, createdByUserId: TestHelpers.UserId1,
                userId: TestHelpers.UserId1);
            await participantRepo.AddAsync(participant);
        }

        var msgRepo = TestHelpers.CreateMessageRepo(db);
        var outboundMsg = Message.Create(
            convId, TestHelpers.TenantId, TestHelpers.OrgId,
            Channel.Email, Direction.Outbound,
            "Reply to external", VisibilityType.SharedExternal,
            TestHelpers.UserId1,
            senderUserId: TestHelpers.UserId1,
            senderParticipantType: ParticipantType.InternalUser);
        await msgRepo.AddAsync(outboundMsg);

        var outboundResult = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(convId, outboundMsg.Id, "external@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.NotNull(outboundResult.MatchedReplyReferenceId);
        Assert.NotNull(outboundResult.GeneratedInternetMessageId);

        var outboundPayload = notifClient.SentPayloads[0];
        Assert.Equal("<e2e-inbound@example.com>", outboundPayload.InReplyToMessageId);
        Assert.Contains("<e2e-inbound@example.com>", outboundPayload.ReferencesHeader!);

        var inboundReply = await intakeService.ProcessInboundAsync(
            new InboundEmailIntakeRequest(
                Provider: "test",
                InternetMessageId: "<e2e-reply2@example.com>",
                ProviderMessageId: null,
                ProviderThreadId: null,
                InReplyToMessageId: outboundResult.GeneratedInternetMessageId,
                ReferencesHeader: $"<e2e-inbound@example.com> {outboundResult.GeneratedInternetMessageId}",
                FromEmail: "external@example.com",
                FromDisplayName: "External User",
                ToAddresses: "inbox@tenant.com",
                CcAddresses: null,
                Subject: "Re: E2E Thread Test",
                TextBody: "Thanks for responding",
                HtmlBody: null,
                ReceivedAtUtc: DateTime.UtcNow,
                TenantId: TestHelpers.TenantId,
                OrgId: TestHelpers.OrgId));

        Assert.False(inboundReply.CreatedNewConversation);
        Assert.Equal(convId, inboundReply.ConversationId);
    }

    [Fact]
    public async Task Test12_NotificationsRequestIdCorrelation_MatchesByNotifReqId()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.NotNull(result.NotificationsRequestId);

        var callbackByNotifId = new DeliveryStatusUpdateRequest(
            Provider: "sendgrid",
            ProviderMessageId: null,
            InternetMessageId: null,
            Status: "delivered",
            StatusAtUtc: DateTime.UtcNow.AddSeconds(5),
            NotificationsRequestId: result.NotificationsRequestId.ToString());

        var matched = await service.ProcessDeliveryStatusAsync(callbackByNotifId, TestHelpers.TenantId);
        Assert.True(matched);

        var states = await service.ListDeliveryStatesAsync(TestHelpers.TenantId, conv.Id, TestHelpers.UserId1);
        Assert.Single(states);
        Assert.Equal(DeliveryStatus.Delivered, states[0].DeliveryStatus);
    }

    [Fact]
    public async Task Test13_DuplicateSendProtection_SecondSendForSameMessageRejected()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);

        await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendOutboundAsync(
                new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
                TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1));

        Assert.Single(notifClient.SentPayloads);
    }

    [Fact]
    public async Task Test14_UnknownStatusMapsToUnknown()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id, "recipient@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        var weirdUpdate = new DeliveryStatusUpdateRequest(
            Provider: "custom-provider",
            ProviderMessageId: null,
            InternetMessageId: result.GeneratedInternetMessageId,
            Status: "something_unexpected",
            StatusAtUtc: DateTime.UtcNow.AddSeconds(5));

        var matched = await service.ProcessDeliveryStatusAsync(weirdUpdate, TestHelpers.TenantId);
        Assert.True(matched);

        var states = await service.ListDeliveryStatesAsync(TestHelpers.TenantId, conv.Id, TestHelpers.UserId1);
        Assert.Single(states);
        Assert.Equal(DeliveryStatus.Unknown, states[0].DeliveryStatus);
    }
}
