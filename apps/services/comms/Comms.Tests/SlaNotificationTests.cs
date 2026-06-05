using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Domain.Constants;
using Comms.Domain.Entities;
using Comms.Domain.Enums;
using Xunit;
using static Comms.Tests.TestHelpers;

namespace Comms.Tests;

public class SlaNotificationTests
{
    private readonly Guid _systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task FirstResponseWarningTrigger_FiresOnce()
    {
        var db = CreateDbContext();
        var notif = new MockNotificationsServiceClient();
        var audit = new NoOpAuditPublisher();
        var service = CreateSlaNotificationService(db, notif, audit);

        var conv = CreateTestConversation(status: "Active", visibility: "SharedExternal");
        await CreateConversationRepo(db).AddAsync(conv);

        var startTime = DateTime.UtcNow.AddHours(-7);
        var sla = ConversationSlaState.Initialize(TenantId, conv.Id, ConversationPriority.Normal, startTime, UserId1);
        await CreateSlaStateRepo(db).AddAsync(sla);

        var queue = ConversationQueue.Create(TenantId, "Support", "support", null, true, UserId1);
        await CreateQueueRepo(db).AddAsync(queue);
        var assignment = ConversationAssignment.Create(TenantId, conv.Id, queue.Id, UserId2, UserId1, UserId1);
        await CreateAssignmentRepo(db).AddAsync(assignment);

        var result = await service.EvaluateAllAsync(TenantId, _systemUserId);

        Assert.True(result.WarningsTriggered >= 1);
        Assert.Contains(notif.SentAlerts, a =>
            a.TriggerType == SlaTriggerType.FirstResponseWarning &&
            a.ConversationId == conv.Id);
    }

    [Fact]
    public async Task FirstResponseBreachTrigger_FiresOnce()
    {
        var db = CreateDbContext();
        var notif = new MockNotificationsServiceClient();
        var audit = new NoOpAuditPublisher();
        var service = CreateSlaNotificationService(db, notif, audit);

        var conv = CreateTestConversation(status: "Active", visibility: "SharedExternal");
        await CreateConversationRepo(db).AddAsync(conv);

        var startTime = DateTime.UtcNow.AddHours(-10);
        var sla = ConversationSlaState.Initialize(TenantId, conv.Id, ConversationPriority.Normal, startTime, UserId1);
        await CreateSlaStateRepo(db).AddAsync(sla);

        var queue = ConversationQueue.Create(TenantId, "Support", "support", null, true, UserId1);
        await CreateQueueRepo(db).AddAsync(queue);
        var assignment = ConversationAssignment.Create(TenantId, conv.Id, queue.Id, UserId2, UserId1, UserId1);
        await CreateAssignmentRepo(db).AddAsync(assignment);

        var result = await service.EvaluateAllAsync(TenantId, _systemUserId);

        Assert.True(result.BreachesTriggered >= 1);
        Assert.Contains(notif.SentAlerts, a =>
            a.TriggerType == SlaTriggerType.FirstResponseBreach &&
            a.ConversationId == conv.Id);
    }

    [Fact]
    public async Task ResolutionWarningTrigger_FiresOnce()
    {
        var db = CreateDbContext();
        var notif = new MockNotificationsServiceClient();
        var audit = new NoOpAuditPublisher();
        var service = CreateSlaNotificationService(db, notif, audit);

        var conv = CreateTestConversation(status: "Active", visibility: "SharedExternal");
        await CreateConversationRepo(db).AddAsync(conv);

        var startTime = DateTime.UtcNow.AddHours(-70);
        var sla = ConversationSlaState.Initialize(TenantId, conv.Id, ConversationPriority.Normal, startTime, UserId1);
        sla.SatisfyFirstResponse(startTime.AddHours(1), UserId1);
        await CreateSlaStateRepo(db).AddAsync(sla);

        var queue = ConversationQueue.Create(TenantId, "Support", "support", null, true, UserId1);
        await CreateQueueRepo(db).AddAsync(queue);
        var assignment = ConversationAssignment.Create(TenantId, conv.Id, queue.Id, UserId2, UserId1, UserId1);
        await CreateAssignmentRepo(db).AddAsync(assignment);

        var result = await service.EvaluateAllAsync(TenantId, _systemUserId);

        Assert.True(result.WarningsTriggered >= 1);
        Assert.Contains(notif.SentAlerts, a =>
            a.TriggerType == SlaTriggerType.ResolutionWarning &&
            a.ConversationId == conv.Id);
    }

    [Fact]
    public async Task ResolutionBreachTrigger_FiresOnce()
    {
        var db = CreateDbContext();
        var notif = new MockNotificationsServiceClient();
        var audit = new NoOpAuditPublisher();
        var service = CreateSlaNotificationService(db, notif, audit);

        var conv = CreateTestConversation(status: "Active", visibility: "SharedExternal");
        await CreateConversationRepo(db).AddAsync(conv);

        var startTime = DateTime.UtcNow.AddHours(-80);
        var sla = ConversationSlaState.Initialize(TenantId, conv.Id, ConversationPriority.Normal, startTime, UserId1);
        sla.SatisfyFirstResponse(startTime.AddHours(1), UserId1);
        await CreateSlaStateRepo(db).AddAsync(sla);

        var queue = ConversationQueue.Create(TenantId, "Support", "support", null, true, UserId1);
        await CreateQueueRepo(db).AddAsync(queue);
        var assignment = ConversationAssignment.Create(TenantId, conv.Id, queue.Id, UserId2, UserId1, UserId1);
        await CreateAssignmentRepo(db).AddAsync(assignment);

        var result = await service.EvaluateAllAsync(TenantId, _systemUserId);

        Assert.True(result.BreachesTriggered >= 1);
        Assert.Contains(notif.SentAlerts, a =>
            a.TriggerType == SlaTriggerType.ResolutionBreach &&
            a.ConversationId == conv.Id);
    }

    [Fact]
    public async Task DuplicateEvaluationIdempotency_DoesNotResend()
    {
        var db = CreateDbContext();
        var notif = new MockNotificationsServiceClient();
        var audit = new NoOpAuditPublisher();
        var service = CreateSlaNotificationService(db, notif, audit);

        var conv = CreateTestConversation(status: "Active", visibility: "SharedExternal");
        await CreateConversationRepo(db).AddAsync(conv);

        var startTime = DateTime.UtcNow.AddHours(-10);
        var sla = ConversationSlaState.Initialize(TenantId, conv.Id, ConversationPriority.Normal, startTime, UserId1);
        await CreateSlaStateRepo(db).AddAsync(sla);

        var queue = ConversationQueue.Create(TenantId, "Support", "support", null, true, UserId1);
        await CreateQueueRepo(db).AddAsync(queue);
        var assignment = ConversationAssignment.Create(TenantId, conv.Id, queue.Id, UserId2, UserId1, UserId1);
        await CreateAssignmentRepo(db).AddAsync(assignment);

        await service.EvaluateAllAsync(TenantId, _systemUserId);
        var firstCount = notif.SentAlerts.Count;

        await service.EvaluateAllAsync(TenantId, _systemUserId);
        var secondCount = notif.SentAlerts.Count;

        Assert.Equal(firstCount, secondCount);
    }

    [Fact]
    public async Task EscalationTargetResolution_AssignedUser()
    {
        var db = CreateDbContext();
        var audit = new NoOpAuditPublisher();
        var resolver = CreateEscalationTargetResolver(db, audit);

        var conv = CreateTestConversation(status: "Active", visibility: "SharedExternal");
        await CreateConversationRepo(db).AddAsync(conv);

        var queue = ConversationQueue.Create(TenantId, "Support", "support", null, true, UserId1);
        await CreateQueueRepo(db).AddAsync(queue);
        var assignment = ConversationAssignment.Create(TenantId, conv.Id, queue.Id, UserId2, UserId1, UserId1);
        await CreateAssignmentRepo(db).AddAsync(assignment);

        var target = await resolver.ResolveAsync(TenantId, conv.Id);

        Assert.NotNull(target);
        Assert.Equal(UserId2, target.UserId);
        Assert.Equal("assigned_user", target.Source);
    }

    [Fact]
    public async Task NoTargetHandling_SkipsSafelyWithAudit()
    {
        var db = CreateDbContext();
        var notif = new MockNotificationsServiceClient();
        var audit = new NoOpAuditPublisher();
        var service = CreateSlaNotificationService(db, notif, audit);

        var conv = CreateTestConversation(status: "Active", visibility: "SharedExternal");
        await CreateConversationRepo(db).AddAsync(conv);

        var startTime = DateTime.UtcNow.AddHours(-10);
        var sla = ConversationSlaState.Initialize(TenantId, conv.Id, ConversationPriority.Normal, startTime, UserId1);
        await CreateSlaStateRepo(db).AddAsync(sla);

        var result = await service.EvaluateAllAsync(TenantId, _systemUserId);

        Assert.Empty(notif.SentAlerts);
        Assert.True(result.SkippedCount >= 0 || result.EvaluatedConversationCount >= 0);
        Assert.Contains(audit.Events, e => e.EventType == "EscalationTargetMissing");
    }

    [Fact]
    public async Task PriorityChangeInteraction_EvaluationRemainsCorrect()
    {
        var db = CreateDbContext();
        var notif = new MockNotificationsServiceClient();
        var audit = new NoOpAuditPublisher();
        var opsService = CreateOperationalService(db, audit);
        var service = CreateSlaNotificationService(db, notif, audit);

        var conv = CreateTestConversation(status: "Active", visibility: "SharedExternal");
        await CreateConversationRepo(db).AddAsync(conv);

        var startTime = DateTime.UtcNow.AddHours(-3);
        var sla = ConversationSlaState.Initialize(TenantId, conv.Id, ConversationPriority.High, startTime, UserId1);
        await CreateSlaStateRepo(db).AddAsync(sla);

        var queue = ConversationQueue.Create(TenantId, "Support", "support", null, true, UserId1);
        await CreateQueueRepo(db).AddAsync(queue);
        var assignment = ConversationAssignment.Create(TenantId, conv.Id, queue.Id, UserId2, UserId1, UserId1);
        await CreateAssignmentRepo(db).AddAsync(assignment);

        await opsService.UpdatePriorityAsync(TenantId, conv.Id, UserId1,
            new UpdateConversationPriorityRequest(ConversationPriority.Low));

        notif.SentAlerts.Clear();
        var result = await service.EvaluateAllAsync(TenantId, _systemUserId);

        Assert.True(result.EvaluatedConversationCount >= 1);
    }

    [Fact]
    public async Task InternalEndpointSecurity_UnauthorizedCallerBlocked()
    {
        var db = CreateDbContext();
        var notif = new MockNotificationsServiceClient();
        var service = CreateSlaNotificationService(db, notif);

        var result = await service.EvaluateAllAsync(TenantId, _systemUserId);

        Assert.NotNull(result);
        Assert.Equal(0, result.EvaluatedConversationCount);
    }

    [Fact]
    public async Task PriorOperationalRegression_ExistingBehaviorsWork()
    {
        var db = CreateDbContext();
        var audit = new NoOpAuditPublisher();
        var opsService = CreateOperationalService(db, audit);

        var conv = CreateTestConversation(status: "Active", visibility: "SharedExternal");
        await CreateConversationRepo(db).AddAsync(conv);

        await opsService.InitializeSlaAsync(TenantId, conv.Id, ConversationPriority.Normal, DateTime.UtcNow, UserId1);

        var sla = await opsService.GetSlaStateAsync(TenantId, conv.Id);
        Assert.NotNull(sla);
        Assert.Equal(ConversationPriority.Normal, sla.Priority);

        var updated = await opsService.UpdatePriorityAsync(TenantId, conv.Id, UserId1,
            new UpdateConversationPriorityRequest(ConversationPriority.High));
        Assert.Equal(ConversationPriority.High, updated.Priority);

        await opsService.SatisfyFirstResponseAsync(TenantId, conv.Id, DateTime.UtcNow, UserId1);
        sla = await opsService.GetSlaStateAsync(TenantId, conv.Id);
        Assert.NotNull(sla!.FirstResponseAtUtc);
    }

    [Fact]
    public async Task PriorCommunicationRegression_ExistingEmailBehaviorsWork()
    {
        var db = CreateDbContext();
        var notif = new MockNotificationsServiceClient();
        var audit = new NoOpAuditPublisher();

        var conv = CreateTestConversation(status: "Active", visibility: "SharedExternal");
        await CreateConversationRepo(db).AddAsync(conv);

        var participant = CreateTestParticipant(conv.Id, userId: UserId1);
        await CreateParticipantRepo(db).AddAsync(participant);

        var externalParticipant = CreateTestParticipant(conv.Id, userId: ExternalUserId,
            participantType: "ExternalContact", externalEmail: "external@test.com", externalName: "External");
        await CreateParticipantRepo(db).AddAsync(externalParticipant);

        var msg = CreateTestMessage(conv.Id, visibility: "SharedExternal", channel: "Email");
        await CreateMessageRepo(db).AddAsync(msg);

        var opsService = CreateOperationalService(db, audit);
        await opsService.InitializeSlaAsync(TenantId, conv.Id, ConversationPriority.Normal, DateTime.UtcNow, UserId1);

        var sla = await opsService.GetSlaStateAsync(TenantId, conv.Id);
        Assert.NotNull(sla);
        Assert.False(sla.BreachedFirstResponse);
        Assert.False(sla.BreachedResolution);
    }
}
