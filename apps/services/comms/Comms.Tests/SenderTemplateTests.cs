using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Services;
using Comms.Domain.Entities;
using Comms.Domain.Enums;
using Xunit;

namespace Comms.Tests;

public class SenderTemplateTests
{
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

    private static SenderConfigService CreateSenderConfigService(
        Infrastructure.Persistence.CommsDbContext db,
        NoOpAuditPublisher? audit = null)
    {
        return new SenderConfigService(
            TestHelpers.CreateSenderConfigRepo(db),
            audit ?? new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<SenderConfigService>());
    }

    private static EmailTemplateService CreateTemplateService(
        Infrastructure.Persistence.CommsDbContext db,
        NoOpAuditPublisher? audit = null)
    {
        return new EmailTemplateService(
            TestHelpers.CreateTemplateConfigRepo(db),
            audit ?? new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<EmailTemplateService>());
    }

    private static async Task<(Conversation conv, Message msg, ConversationParticipant participant)> SeedConversation(
        Infrastructure.Persistence.CommsDbContext db)
    {
        var convRepo = TestHelpers.CreateConversationRepo(db);
        var msgRepo = TestHelpers.CreateMessageRepo(db);
        var partRepo = TestHelpers.CreateParticipantRepo(db);

        var conv = Conversation.Create(
            TestHelpers.TenantId, TestHelpers.OrgId, "SYNQ_COMMS",
            ContextType.Case, "case-st-1",
            "Sender/Template Test", VisibilityType.SharedExternal, TestHelpers.UserId1);
        await convRepo.AddAsync(conv);

        var participant = ConversationParticipant.Create(
            conv.Id, TestHelpers.TenantId, TestHelpers.OrgId,
            ParticipantType.InternalUser, ParticipantRole.Participant, true,
            TestHelpers.UserId1, userId: TestHelpers.UserId1);
        await partRepo.AddAsync(participant);

        var msg = Message.Create(
            conv.Id, TestHelpers.TenantId, TestHelpers.OrgId,
            Channel.Email, Direction.Outbound,
            "Test body content", VisibilityType.SharedExternal,
            TestHelpers.UserId1,
            senderUserId: TestHelpers.UserId1,
            senderParticipantType: ParticipantType.InternalUser);
        await msgRepo.AddAsync(msg);

        return (conv, msg, participant);
    }

    private static async Task<TenantEmailSenderConfig> SeedVerifiedSenderConfig(
        Infrastructure.Persistence.CommsDbContext db,
        string fromEmail = "support@tenant.com",
        string displayName = "Tenant Support",
        bool isDefault = true,
        string senderType = "SUPPORT")
    {
        var repo = TestHelpers.CreateSenderConfigRepo(db);
        var config = TenantEmailSenderConfig.Create(
            TestHelpers.TenantId, displayName, fromEmail, senderType,
            TestHelpers.UserId1, isDefault: isDefault,
            verificationStatus: VerificationStatus.Verified);
        await repo.AddAsync(config);
        await repo.SaveChangesAsync();
        return config;
    }

    private static async Task<EmailTemplateConfig> SeedTemplate(
        Infrastructure.Persistence.CommsDbContext db,
        string templateKey,
        string scope = "TENANT",
        string? subjectTemplate = null,
        string? bodyTextTemplate = null,
        string? bodyHtmlTemplate = null,
        bool isDefault = false)
    {
        var repo = TestHelpers.CreateTemplateConfigRepo(db);
        var config = EmailTemplateConfig.Create(
            templateKey, $"Template {templateKey}", scope,
            TestHelpers.UserId1,
            tenantId: scope == TemplateScope.Tenant ? TestHelpers.TenantId : null,
            subjectTemplate: subjectTemplate,
            bodyTextTemplate: bodyTextTemplate,
            bodyHtmlTemplate: bodyHtmlTemplate,
            isDefault: isDefault);
        await repo.AddAsync(config);
        await repo.SaveChangesAsync();
        return config;
    }

    [Fact]
    public async Task Test1_DefaultSenderConfigResolution_UsesDefaultVerifiedSender()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateOutboundService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);
        await SeedVerifiedSenderConfig(db, "default@tenant.com", "Default Sender", isDefault: true);

        var request = new SendOutboundEmailRequest(conv.Id, msg.Id, "external@example.com");
        var result = await service.SendOutboundAsync(request, TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.NotNull(result.SenderConfigId);
        Assert.Equal("default@tenant.com", result.SenderEmail);
        Assert.Equal("default@tenant.com", notifClient.SentPayloads[0].FromEmail);
        Assert.Equal("Default Sender", notifClient.SentPayloads[0].FromDisplayName);
    }

    [Fact]
    public async Task Test2_InvalidSenderConfigRejected_InactiveOrUnverified()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateOutboundService(db);
        var (conv, msg, _) = await SeedConversation(db);

        var repo = TestHelpers.CreateSenderConfigRepo(db);
        var unverified = TenantEmailSenderConfig.Create(
            TestHelpers.TenantId, "Unverified", "unverified@tenant.com",
            SenderType.Support, TestHelpers.UserId1,
            verificationStatus: VerificationStatus.Pending);
        await repo.AddAsync(unverified);
        await repo.SaveChangesAsync();

        var request = new SendOutboundEmailRequest(conv.Id, msg.Id, "external@example.com",
            SenderConfigId: unverified.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendOutboundAsync(request, TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1));
        Assert.Contains("must be active and verified", ex.Message);
    }

    [Fact]
    public async Task Test3_TemplateResolutionByKey_ResolvesCorrectTemplate()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateOutboundService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);

        await SeedTemplate(db, "welcome_email",
            subjectTemplate: "Welcome {{name}}",
            bodyTextTemplate: "Hello {{name}}, welcome to {{company}}");

        var request = new SendOutboundEmailRequest(conv.Id, msg.Id, "external@example.com",
            TemplateKey: "welcome_email",
            TemplateVariables: new Dictionary<string, string>
            {
                ["name"] = "John",
                ["company"] = "Acme Corp"
            });

        var result = await service.SendOutboundAsync(request, TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.Equal("welcome_email", result.TemplateKey);
        Assert.NotNull(result.TemplateConfigId);
        Assert.Equal("TEMPLATE", result.CompositionMode);
        Assert.Equal("Welcome John", result.RenderedSubject);
        Assert.Equal("Welcome John", notifClient.SentPayloads[0].Subject);
        Assert.Equal("Hello John, welcome to Acme Corp", notifClient.SentPayloads[0].BodyText);
    }

    [Fact]
    public async Task Test4_TenantTemplateOverridesGlobal_SameKey()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateOutboundService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);

        await SeedTemplate(db, "notification", scope: TemplateScope.Global,
            subjectTemplate: "Global: {{topic}}");
        await SeedTemplate(db, "notification", scope: TemplateScope.Tenant,
            subjectTemplate: "Tenant: {{topic}}");

        var request = new SendOutboundEmailRequest(conv.Id, msg.Id, "external@example.com",
            TemplateKey: "notification",
            TemplateVariables: new Dictionary<string, string> { ["topic"] = "Update" });

        var result = await service.SendOutboundAsync(request, TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.Equal("Tenant: Update", result.RenderedSubject);
    }

    [Fact]
    public async Task Test5_TemplateRenderingApplied_VariablesReplacedCorrectly()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateOutboundService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);

        await SeedTemplate(db, "case_update",
            subjectTemplate: "Case {{caseId}} - {{status}}",
            bodyTextTemplate: "Dear {{clientName}}, your case {{caseId}} status is now {{status}}.",
            bodyHtmlTemplate: "<p>Dear <b>{{clientName}}</b>, case {{caseId}} is {{status}}.</p>");

        var vars = new Dictionary<string, string>
        {
            ["caseId"] = "LGL-2024-001",
            ["status"] = "Under Review",
            ["clientName"] = "Jane Doe"
        };

        var request = new SendOutboundEmailRequest(conv.Id, msg.Id, "external@example.com",
            TemplateKey: "case_update", TemplateVariables: vars);

        var result = await service.SendOutboundAsync(request, TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.Equal("Case LGL-2024-001 - Under Review", notifClient.SentPayloads[0].Subject);
        Assert.Contains("Jane Doe", notifClient.SentPayloads[0].BodyText!);
        Assert.Contains("<b>Jane Doe</b>", notifClient.SentPayloads[0].BodyHtml!);
    }

    [Fact]
    public async Task Test6_ExplicitOverridePrecedence_OverrideBeatsTemplate()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateOutboundService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);

        await SeedTemplate(db, "base_template",
            subjectTemplate: "Template Subject",
            bodyTextTemplate: "Template Body");

        var request = new SendOutboundEmailRequest(conv.Id, msg.Id, "external@example.com",
            TemplateKey: "base_template",
            SubjectOverride: "Overridden Subject",
            BodyTextOverride: "Overridden Body");

        var result = await service.SendOutboundAsync(request, TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.Equal("EXPLICIT_OVERRIDE", result.CompositionMode);
        Assert.Equal("Overridden Subject", result.RenderedSubject);
        Assert.Equal("Overridden Subject", notifClient.SentPayloads[0].Subject);
        Assert.Equal("Overridden Body", notifClient.SentPayloads[0].BodyText);
    }

    [Fact]
    public async Task Test7_SenderTemplateMetadataPersisted_OnEmailReference()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateOutboundService(db);
        var (conv, msg, _) = await SeedConversation(db);
        var senderConfig = await SeedVerifiedSenderConfig(db);
        var template = await SeedTemplate(db, "status_update",
            subjectTemplate: "Status Update");

        var request = new SendOutboundEmailRequest(conv.Id, msg.Id, "external@example.com",
            SenderConfigId: senderConfig.Id,
            TemplateKey: "status_update");

        var result = await service.SendOutboundAsync(request, TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.Equal(senderConfig.Id, result.SenderConfigId);
        Assert.Equal("support@tenant.com", result.SenderEmail);
        Assert.Equal("status_update", result.TemplateKey);
        Assert.Equal(template.Id, result.TemplateConfigId);
        Assert.Equal("TEMPLATE", result.CompositionMode);

        var emailRefRepo = TestHelpers.CreateEmailRefRepo(db);
        var emailRef = await emailRefRepo.GetByIdAsync(TestHelpers.TenantId, result.EmailMessageReferenceId);
        Assert.NotNull(emailRef);
        Assert.Equal(senderConfig.Id, emailRef!.SenderConfigId);
        Assert.Equal("support@tenant.com", emailRef.SenderConfigEmail);
        Assert.Equal(template.Id, emailRef.TemplateConfigId);
        Assert.Equal("status_update", emailRef.TemplateKey);
        Assert.Equal("TEMPLATE", emailRef.CompositionMode);
    }

    [Fact]
    public async Task Test8_NotificationsPayloadIncludesSenderData()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateOutboundService(db, notifClient);
        var (conv, msg, _) = await SeedConversation(db);

        var repo = TestHelpers.CreateSenderConfigRepo(db);
        var config = TenantEmailSenderConfig.Create(
            TestHelpers.TenantId, "Operations Team", "ops@tenant.com",
            SenderType.Operations, TestHelpers.UserId1,
            replyToEmail: "replies@tenant.com",
            isDefault: true,
            verificationStatus: VerificationStatus.Verified);
        await repo.AddAsync(config);
        await repo.SaveChangesAsync();

        var request = new SendOutboundEmailRequest(conv.Id, msg.Id, "external@example.com");
        await service.SendOutboundAsync(request, TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        var payload = notifClient.SentPayloads[0];
        Assert.Equal("ops@tenant.com", payload.FromEmail);
        Assert.Equal("Operations Team", payload.FromDisplayName);
        Assert.Equal("replies@tenant.com", payload.ReplyToEmail);
    }

    [Fact]
    public async Task Test9_AuthorizationVisibilityRulesStillHold_InternalOnlyCannotSend()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateOutboundService(db);

        var convRepo = TestHelpers.CreateConversationRepo(db);
        var msgRepo = TestHelpers.CreateMessageRepo(db);
        var partRepo = TestHelpers.CreateParticipantRepo(db);

        var conv = Conversation.Create(
            TestHelpers.TenantId, TestHelpers.OrgId, "SYNQ_COMMS",
            ContextType.Case, "case-auth-1",
            "Internal Conv", VisibilityType.InternalOnly, TestHelpers.UserId1);
        await convRepo.AddAsync(conv);

        var participant = ConversationParticipant.Create(
            conv.Id, TestHelpers.TenantId, TestHelpers.OrgId,
            ParticipantType.InternalUser, ParticipantRole.Participant, true,
            TestHelpers.UserId1, userId: TestHelpers.UserId1);
        await partRepo.AddAsync(participant);

        var msg = Message.Create(
            conv.Id, TestHelpers.TenantId, TestHelpers.OrgId,
            Channel.InApp, Direction.Internal,
            "Internal message", VisibilityType.InternalOnly,
            TestHelpers.UserId1,
            senderUserId: TestHelpers.UserId1,
            senderParticipantType: ParticipantType.InternalUser);
        await msgRepo.AddAsync(msg);

        var request = new SendOutboundEmailRequest(conv.Id, msg.Id, "external@example.com");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendOutboundAsync(request, TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1));
    }

    [Fact]
    public async Task Test10_AuditEventsEmitted_ForSenderAndTemplateOperations()
    {
        var db = TestHelpers.CreateDbContext();
        var audit = new NoOpAuditPublisher();
        var senderService = CreateSenderConfigService(db, audit);
        var templateService = CreateTemplateService(db, audit);

        await senderService.CreateAsync(
            new CreateTenantEmailSenderConfigRequest("Test Sender", "test@tenant.com", "SUPPORT"),
            TestHelpers.TenantId, TestHelpers.UserId1);

        Assert.Contains(audit.Events, e => e.EventType == "SenderConfigCreated");

        await templateService.CreateAsync(
            new CreateEmailTemplateConfigRequest("test_template", "Test Template", "TENANT",
                SubjectTemplate: "Hello {{name}}"),
            TestHelpers.TenantId, TestHelpers.UserId1);

        Assert.Contains(audit.Events, e => e.EventType == "TemplateCreated");

        var audit2 = new NoOpAuditPublisher();
        var outboundService = CreateOutboundService(db, audit: audit2);
        var (conv, msg, _) = await SeedConversation(db);

        var senderRepo = TestHelpers.CreateSenderConfigRepo(db);
        var senderConfig = TenantEmailSenderConfig.Create(
            TestHelpers.TenantId, "Verified Sender", "verified@tenant.com",
            SenderType.Support, TestHelpers.UserId1,
            isDefault: true,
            verificationStatus: VerificationStatus.Verified);
        await senderRepo.AddAsync(senderConfig);
        await senderRepo.SaveChangesAsync();

        var request = new SendOutboundEmailRequest(conv.Id, msg.Id, "external@example.com",
            TemplateKey: "test_template",
            TemplateVariables: new Dictionary<string, string> { ["name"] = "User" });

        await outboundService.SendOutboundAsync(request, TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.Contains(audit2.Events, e => e.EventType == "OutboundEmailSenderResolved");
        Assert.Contains(audit2.Events, e => e.EventType == "OutboundEmailTemplateResolved");
        Assert.Contains(audit2.Events, e => e.EventType == "OutboundEmailQueued");
    }

    [Fact]
    public async Task Test11_CrossTenantTemplateAccessDenied()
    {
        var db = TestHelpers.CreateDbContext();
        var audit = new NoOpAuditPublisher();
        var templateService = CreateTemplateService(db, audit);

        var otherTenantId = Guid.NewGuid();
        var templateRepo = TestHelpers.CreateTemplateConfigRepo(db);
        var otherTemplate = EmailTemplateConfig.Create(
            "other_tenant_tmpl", "Other Tenant Template",
            "TENANT", TestHelpers.UserId1,
            tenantId: otherTenantId,
            subjectTemplate: "Secret {{data}}");
        await templateRepo.AddAsync(otherTemplate);
        await templateRepo.SaveChangesAsync();

        var result = await templateService.GetByIdAsync(otherTemplate.Id, TestHelpers.TenantId);
        Assert.Null(result);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            templateService.UpdateAsync(otherTemplate.Id,
                new UpdateEmailTemplateConfigRequest(DisplayName: "Hacked"),
                TestHelpers.TenantId, TestHelpers.UserId1));
    }

    [Fact]
    public async Task Test12_GlobalTemplateCreateBlockedFromTenantEndpoint()
    {
        var db = TestHelpers.CreateDbContext();
        var audit = new NoOpAuditPublisher();
        var templateService = CreateTemplateService(db, audit);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            templateService.CreateAsync(
                new CreateEmailTemplateConfigRequest("global_tmpl", "Global Template", "GLOBAL",
                    SubjectTemplate: "{{topic}}"),
                TestHelpers.TenantId, TestHelpers.UserId1));
    }

    [Fact]
    public async Task Test13_PriorOutboundThreadingStillWorks_AfterSenderTemplateAdditions()
    {
        var db = TestHelpers.CreateDbContext();
        var notifClient = new MockNotificationsServiceClient();
        var service = CreateOutboundService(db, notifClient);
        var (conv, msg1, _) = await SeedConversation(db);

        var result1 = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg1.Id, "external@example.com"),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.NotNull(result1.GeneratedInternetMessageId);
        Assert.Contains("@comms.legalsynq.com>", result1.GeneratedInternetMessageId);
        Assert.Equal("MESSAGE_CONTENT", result1.CompositionMode);

        var msgRepo = TestHelpers.CreateMessageRepo(db);
        var msg2 = Message.Create(
            conv.Id, TestHelpers.TenantId, TestHelpers.OrgId,
            Channel.Email, Direction.Outbound,
            "Follow-up body", VisibilityType.SharedExternal,
            TestHelpers.UserId1,
            senderUserId: TestHelpers.UserId1,
            senderParticipantType: ParticipantType.InternalUser);
        await msgRepo.AddAsync(msg2);

        var result2 = await service.SendOutboundAsync(
            new SendOutboundEmailRequest(conv.Id, msg2.Id, "external@example.com",
                ReplyToEmailReferenceId: result1.EmailMessageReferenceId),
            TestHelpers.TenantId, TestHelpers.OrgId, TestHelpers.UserId1);

        Assert.NotNull(result2.MatchedReplyReferenceId);
        Assert.Equal(result1.EmailMessageReferenceId, result2.MatchedReplyReferenceId);

        var secondPayload = notifClient.SentPayloads[1];
        Assert.Equal(result1.GeneratedInternetMessageId, secondPayload.InReplyToMessageId);
        Assert.Contains(result1.GeneratedInternetMessageId, secondPayload.ReferencesHeader!);
    }
}
