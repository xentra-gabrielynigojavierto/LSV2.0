using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Services;
using Comms.Domain.Entities;
using Comms.Domain.Enums;
using Xunit;

namespace Comms.Tests;

public class CcBccRecipientTests
{
    private static EmailIntakeService CreateIntakeService(
        Infrastructure.Persistence.CommsDbContext db,
        NoOpAuditPublisher? audit = null)
    {
        return new EmailIntakeService(
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
            audit ?? new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<EmailIntakeService>());
    }

    private static OutboundEmailService CreateOutboundService(
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
            ContextType.General, $"test-{Guid.NewGuid():N}",
            "CC/BCC Test Subject", VisibilityType.SharedExternal,
            TestHelpers.UserId1);
        await convRepo.AddAsync(conv);

        var msg = Message.Create(
            conv.Id, TestHelpers.TenantId, TestHelpers.OrgId,
            Channel.Email, Direction.Outbound,
            "Outbound body", VisibilityType.SharedExternal,
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
    public async Task InboundCcRecipients_PersistAsVisibleRecords()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateIntakeService(db);

        var result = await service.ProcessInboundAsync(
            new InboundEmailIntakeRequest(
                Provider: "test",
                InternetMessageId: $"<cc-test-{Guid.NewGuid():N}@example.com>",
                ProviderMessageId: null,
                ProviderThreadId: null,
                InReplyToMessageId: null,
                ReferencesHeader: null,
                FromEmail: "sender@example.com",
                FromDisplayName: "Sender",
                ToAddresses: "inbox@tenant.com",
                CcAddresses: "cc1@example.com, cc2@example.com",
                Subject: "CC Test",
                TextBody: "Hello with CC",
                HtmlBody: null,
                ReceivedAtUtc: DateTime.UtcNow,
                TenantId: TestHelpers.TenantId,
                OrgId: TestHelpers.OrgId));

        Assert.True(result.CreatedNewConversation);

        var recipientRepo = TestHelpers.CreateRecipientRepo(db);
        var recipients = await recipientRepo.ListVisibleByConversationAsync(
            TestHelpers.TenantId, result.ConversationId);

        Assert.Equal(3, recipients.Count);
        Assert.Contains(recipients, r => r.NormalizedEmail == "inbox@tenant.com" && r.RecipientType == RecipientType.To);
        Assert.Contains(recipients, r => r.NormalizedEmail == "cc1@example.com" && r.RecipientType == RecipientType.Cc);
        Assert.Contains(recipients, r => r.NormalizedEmail == "cc2@example.com" && r.RecipientType == RecipientType.Cc);
    }

    [Fact]
    public async Task InboundCc_ExternalIdentityReused()
    {
        var db = TestHelpers.CreateDbContext();
        var identityRepo = TestHelpers.CreateIdentityRepo(db);

        var existingIdentity = ExternalParticipantIdentity.Create(
            TestHelpers.TenantId, "reuse@example.com", null, "Existing User");
        await identityRepo.AddAsync(existingIdentity);

        var service = CreateIntakeService(db);

        await service.ProcessInboundAsync(
            new InboundEmailIntakeRequest(
                Provider: "test",
                InternetMessageId: $"<reuse-test-{Guid.NewGuid():N}@example.com>",
                ProviderMessageId: null,
                ProviderThreadId: null,
                InReplyToMessageId: null,
                ReferencesHeader: null,
                FromEmail: "sender@example.com",
                FromDisplayName: "Sender",
                ToAddresses: "inbox@tenant.com",
                CcAddresses: "reuse@example.com",
                Subject: "Reuse Test",
                TextBody: "Hello",
                HtmlBody: null,
                ReceivedAtUtc: DateTime.UtcNow,
                TenantId: TestHelpers.TenantId,
                OrgId: TestHelpers.OrgId));

        var identities = await identityRepo.FindByEmailAsync(TestHelpers.TenantId, "reuse@example.com");
        Assert.NotNull(identities);
        Assert.NotNull(identities!.ParticipantId);
    }

    [Fact]
    public async Task OutboundToCcBcc_PassedToNotificationsCorrectly()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateOutboundService(db, notifClient);

        var (conv, msg, _) = await SeedConversation(db);

        var result = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id,
                ToAddresses: "to@example.com",
                CcAddresses: "cc@example.com",
                BccAddresses: "bcc@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.Single(notifClient.SentPayloads);
        var payload = notifClient.SentPayloads[0];
        Assert.Equal("to@example.com", payload.ToAddresses);
        Assert.Equal("cc@example.com", payload.CcAddresses);
        Assert.Equal("bcc@example.com", payload.BccAddresses);
    }

    [Fact]
    public async Task BccNotExposedInVisibleRecipients()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateOutboundService(db, notifClient);

        var (conv, msg, _) = await SeedConversation(db);

        await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id,
                ToAddresses: "to@example.com",
                CcAddresses: "cc@example.com",
                BccAddresses: "hidden@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        var recipientRepo = TestHelpers.CreateRecipientRepo(db);
        var visibleRecipients = await recipientRepo.ListVisibleByConversationAsync(
            TestHelpers.TenantId, conv.Id);

        Assert.DoesNotContain(visibleRecipients, r => r.NormalizedEmail == "hidden@example.com");
        Assert.Contains(visibleRecipients, r => r.NormalizedEmail == "to@example.com");
        Assert.Contains(visibleRecipients, r => r.NormalizedEmail == "cc@example.com");

        var allRecipients = await recipientRepo.ListByEmailReferenceAsync(
            TestHelpers.TenantId, visibleRecipients[0].EmailMessageReferenceId);
        Assert.Contains(allRecipients, r =>
            r.NormalizedEmail == "hidden@example.com" &&
            r.RecipientVisibility == RecipientVisibility.Hidden);
    }

    [Fact]
    public async Task ReplyAllPreview_ExcludesBccAndCurrentSender()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateOutboundService(db, notifClient);

        var (conv, msg, _) = await SeedConversation(db);

        await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id,
                ToAddresses: "to1@example.com, to2@example.com",
                CcAddresses: "cc1@example.com",
                BccAddresses: "secret@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        var preview = await service.GetReplyAllPreviewAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1);

        Assert.NotNull(preview);
        Assert.Equal(conv.Id, preview.ConversationId);

        var allEmails = preview.ToRecipients.Select(r => r.NormalizedEmail)
            .Concat(preview.CcRecipients.Select(r => r.NormalizedEmail))
            .ToList();

        Assert.DoesNotContain("secret@example.com", allEmails);
        Assert.DoesNotContain("noreply@legalsynq.com", allEmails);
        Assert.Contains("to1@example.com", allEmails);
        Assert.Contains("to2@example.com", allEmails);
        Assert.Contains("cc1@example.com", allEmails);
    }

    [Fact]
    public async Task DuplicateRecipients_Deduplicated()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateOutboundService(db, notifClient);

        var (conv, msg, _) = await SeedConversation(db);

        await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg.Id,
                ToAddresses: "user@example.com",
                CcAddresses: "user@example.com",
                BccAddresses: "user@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        var recipientRepo = TestHelpers.CreateRecipientRepo(db);
        var emailRefRepo = TestHelpers.CreateEmailRefRepo(db);
        var refs = await emailRefRepo.ListByConversationAsync(TestHelpers.TenantId, conv.Id);
        var outRef = refs.First(r => r.EmailDirection == EmailDirection.Outbound);

        var records = await recipientRepo.ListByEmailReferenceAsync(
            TestHelpers.TenantId, outRef.Id);

        Assert.Single(records);
        Assert.Equal("user@example.com", records[0].NormalizedEmail);
    }

    [Fact]
    public async Task InternalOnlyMessage_DoesNotExpandExternalParticipants()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateOutboundService(db);

        var convRepo = TestHelpers.CreateConversationRepo(db);
        var msgRepo = TestHelpers.CreateMessageRepo(db);
        var participantRepo = TestHelpers.CreateParticipantRepo(db);

        var conv = Conversation.Create(
            TestHelpers.TenantId, TestHelpers.OrgId, "SYNQ_COMMS",
            ContextType.General, $"test-{Guid.NewGuid():N}",
            "Internal Subject", VisibilityType.InternalOnly,
            TestHelpers.UserId1);
        await convRepo.AddAsync(conv);

        var msg = Message.Create(
            conv.Id, TestHelpers.TenantId, TestHelpers.OrgId,
            Channel.InApp, Direction.Internal,
            "Internal message", VisibilityType.InternalOnly,
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

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendOutboundAsync(
                new SendOutboundEmailRequest(conv.Id, msg.Id,
                    ToAddresses: "external@example.com",
                    CcAddresses: "cc@example.com"),
                TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1));
    }

    [Fact]
    public async Task AuditEvents_EmittedForRecipientExpansion()
    {
        var db = TestHelpers.CreateDbContext();
        var audit = new NoOpAuditPublisher();
        var service = CreateIntakeService(db, audit);

        await service.ProcessInboundAsync(
            new InboundEmailIntakeRequest(
                Provider: "test",
                InternetMessageId: $"<audit-test-{Guid.NewGuid():N}@example.com>",
                ProviderMessageId: null,
                ProviderThreadId: null,
                InReplyToMessageId: null,
                ReferencesHeader: null,
                FromEmail: "sender@example.com",
                FromDisplayName: "Sender",
                ToAddresses: "inbox@tenant.com",
                CcAddresses: "cc-new@example.com",
                Subject: "Audit Test",
                TextBody: "Hello",
                HtmlBody: null,
                ReceivedAtUtc: DateTime.UtcNow,
                TenantId: TestHelpers.TenantId,
                OrgId: TestHelpers.OrgId));

        Assert.Contains(audit.Events, e => e.EventType == "InboundRecipientRecordsCreated");
        Assert.Contains(audit.Events, e => e.EventType == "ExternalParticipantExpandedFromCc");
    }

    [Fact]
    public async Task PriorThreading_StillWorks()
    {
        var db = TestHelpers.CreateDbContext();
        var intakeService = CreateIntakeService(db);

        var first = await intakeService.ProcessInboundAsync(
            new InboundEmailIntakeRequest(
                Provider: "test",
                InternetMessageId: "<regression-first@example.com>",
                ProviderMessageId: null,
                ProviderThreadId: null,
                InReplyToMessageId: null,
                ReferencesHeader: null,
                FromEmail: "sender@example.com",
                FromDisplayName: "Sender",
                ToAddresses: "inbox@tenant.com",
                CcAddresses: null,
                Subject: "Regression",
                TextBody: "First",
                HtmlBody: null,
                ReceivedAtUtc: DateTime.UtcNow,
                TenantId: TestHelpers.TenantId,
                OrgId: TestHelpers.OrgId));

        Assert.True(first.CreatedNewConversation);

        var reply = await intakeService.ProcessInboundAsync(
            new InboundEmailIntakeRequest(
                Provider: "test",
                InternetMessageId: "<regression-reply@example.com>",
                ProviderMessageId: null,
                ProviderThreadId: null,
                InReplyToMessageId: "<regression-first@example.com>",
                ReferencesHeader: null,
                FromEmail: "sender@example.com",
                FromDisplayName: "Sender",
                ToAddresses: "inbox@tenant.com",
                CcAddresses: null,
                Subject: "Re: Regression",
                TextBody: "Reply",
                HtmlBody: null,
                ReceivedAtUtc: DateTime.UtcNow,
                TenantId: TestHelpers.TenantId,
                OrgId: TestHelpers.OrgId));

        Assert.False(reply.CreatedNewConversation);
        Assert.Equal(first.ConversationId, reply.ConversationId);
    }
}
