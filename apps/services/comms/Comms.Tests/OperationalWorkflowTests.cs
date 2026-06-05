using Comms.Application.DTOs;
using Comms.Application.Services;
using Comms.Domain.Enums;
using Xunit;

namespace Comms.Tests;

public class OperationalWorkflowTests
{
    private static QueueService CreateQueueService(
        Infrastructure.Persistence.CommsDbContext db,
        NoOpAuditPublisher? audit = null)
    {
        return new QueueService(
            TestHelpers.CreateQueueRepo(db),
            audit ?? new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<QueueService>());
    }

    private static AssignmentService CreateAssignmentService(
        Infrastructure.Persistence.CommsDbContext db,
        NoOpAuditPublisher? audit = null)
    {
        return new AssignmentService(
            TestHelpers.CreateAssignmentRepo(db),
            TestHelpers.CreateQueueRepo(db),
            TestHelpers.CreateConversationRepo(db),
            TestHelpers.CreateSlaStateRepo(db),
            new NoOpTimelineService(),
            audit ?? new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<AssignmentService>());
    }

    private static OperationalService CreateOperationalService(
        Infrastructure.Persistence.CommsDbContext db,
        NoOpAuditPublisher? audit = null)
    {
        return new OperationalService(
            TestHelpers.CreateSlaStateRepo(db),
            TestHelpers.CreateAssignmentRepo(db),
            TestHelpers.CreateQueueRepo(db),
            TestHelpers.CreateConversationRepo(db),
            new NoOpTimelineService(),
            audit ?? new NoOpAuditPublisher(),
            TestHelpers.CreateLogger<OperationalService>());
    }

    [Fact]
    public async Task CreateQueue_ReturnsQueueWithCode()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateQueueService(db);

        var result = await service.CreateAsync(
            TestHelpers.TenantId, TestHelpers.UserId1,
            new CreateConversationQueueRequest("Support", "SUPPORT", "General support queue", true));

        Assert.Equal("Support", result.Name);
        Assert.Equal("SUPPORT", result.Code);
        Assert.True(result.IsDefault);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task CreateQueue_DuplicateCode_Throws()
    {
        var db = TestHelpers.CreateDbContext();
        var service = CreateQueueService(db);

        await service.CreateAsync(
            TestHelpers.TenantId, TestHelpers.UserId1,
            new CreateConversationQueueRequest("Support", "DUP", null, false));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(
                TestHelpers.TenantId, TestHelpers.UserId1,
                new CreateConversationQueueRequest("Support2", "DUP", null, false)));
    }

    [Fact]
    public async Task AssignConversation_CreatesAssignment()
    {
        var db = TestHelpers.CreateDbContext();
        var queueService = CreateQueueService(db);
        var assignmentService = CreateAssignmentService(db);

        var queue = await queueService.CreateAsync(
            TestHelpers.TenantId, TestHelpers.UserId1,
            new CreateConversationQueueRequest("Support", "SUPPORT", null, true));

        var conv = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conv);

        var assignment = await assignmentService.AssignAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1,
            new AssignConversationRequest(queue.Id, TestHelpers.UserId2, ConversationPriority.High));

        Assert.Equal(conv.Id, assignment.ConversationId);
        Assert.Equal(queue.Id, assignment.QueueId);
        Assert.Equal(TestHelpers.UserId2, assignment.AssignedUserId);
        Assert.Equal(AssignmentStatus.Assigned, assignment.AssignmentStatus);
    }

    [Fact]
    public async Task AssignConversation_AlreadyAssigned_Throws()
    {
        var db = TestHelpers.CreateDbContext();
        var assignmentService = CreateAssignmentService(db);

        var conv = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conv);

        await assignmentService.AssignAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1,
            new AssignConversationRequest(null, TestHelpers.UserId2, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            assignmentService.AssignAsync(
                TestHelpers.TenantId, conv.Id, TestHelpers.UserId1,
                new AssignConversationRequest(null, TestHelpers.UserId1, null)));
    }

    [Fact]
    public async Task ReassignConversation_UpdatesUser()
    {
        var db = TestHelpers.CreateDbContext();
        var assignmentService = CreateAssignmentService(db);

        var conv = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conv);

        await assignmentService.AssignAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1,
            new AssignConversationRequest(null, TestHelpers.UserId1, null));

        var reassigned = await assignmentService.ReassignAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1,
            new ReassignConversationRequest(null, TestHelpers.UserId2));

        Assert.Equal(TestHelpers.UserId2, reassigned.AssignedUserId);
        Assert.Equal(AssignmentStatus.Assigned, reassigned.AssignmentStatus);
    }

    [Fact]
    public async Task AcceptAssignment_SetsAcceptedStatus()
    {
        var db = TestHelpers.CreateDbContext();
        var assignmentService = CreateAssignmentService(db);

        var conv = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conv);

        await assignmentService.AssignAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1,
            new AssignConversationRequest(null, TestHelpers.UserId2, null));

        var accepted = await assignmentService.AcceptAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId2);

        Assert.Equal(AssignmentStatus.Accepted, accepted.AssignmentStatus);
        Assert.NotNull(accepted.AcceptedAtUtc);
    }

    [Fact]
    public async Task AcceptAssignment_WrongUser_Throws()
    {
        var db = TestHelpers.CreateDbContext();
        var assignmentService = CreateAssignmentService(db);

        var conv = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conv);

        await assignmentService.AssignAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1,
            new AssignConversationRequest(null, TestHelpers.UserId2, null));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            assignmentService.AcceptAsync(TestHelpers.TenantId, conv.Id, TestHelpers.UserId1));
    }

    [Fact]
    public async Task UnassignConversation_SetsUnassignedStatus()
    {
        var db = TestHelpers.CreateDbContext();
        var assignmentService = CreateAssignmentService(db);

        var conv = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conv);

        await assignmentService.AssignAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1,
            new AssignConversationRequest(null, TestHelpers.UserId2, null));

        var unassigned = await assignmentService.UnassignAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1);

        Assert.Equal(AssignmentStatus.Unassigned, unassigned.AssignmentStatus);
        Assert.NotNull(unassigned.UnassignedAtUtc);
    }

    [Fact]
    public async Task InitializeSla_SetsDueDates()
    {
        var db = TestHelpers.CreateDbContext();
        var opService = CreateOperationalService(db);

        var conv = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conv);

        var start = DateTime.UtcNow;
        await opService.InitializeSlaAsync(
            TestHelpers.TenantId, conv.Id, ConversationPriority.Normal, start, TestHelpers.UserId1);

        var sla = await opService.GetSlaStateAsync(TestHelpers.TenantId, conv.Id);

        Assert.NotNull(sla);
        Assert.Equal(ConversationPriority.Normal, sla!.Priority);
        Assert.NotNull(sla.FirstResponseDueAtUtc);
        Assert.NotNull(sla.ResolutionDueAtUtc);
        Assert.True(sla.FirstResponseDueAtUtc > start);
        Assert.True(sla.ResolutionDueAtUtc > sla.FirstResponseDueAtUtc);
    }

    [Fact]
    public async Task UpdatePriority_RecalculatesDueDates()
    {
        var db = TestHelpers.CreateDbContext();
        var opService = CreateOperationalService(db);

        var conv = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conv);

        var start = DateTime.UtcNow;
        await opService.InitializeSlaAsync(
            TestHelpers.TenantId, conv.Id, ConversationPriority.Low, start, TestHelpers.UserId1);

        var before = await opService.GetSlaStateAsync(TestHelpers.TenantId, conv.Id);
        Assert.Equal(ConversationPriority.Low, before!.Priority);
        var originalFirstResponseDue = before.FirstResponseDueAtUtc;

        var updated = await opService.UpdatePriorityAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1,
            new UpdateConversationPriorityRequest(ConversationPriority.Urgent));

        Assert.Equal(ConversationPriority.Urgent, updated.Priority);
        Assert.True(updated.FirstResponseDueAtUtc < originalFirstResponseDue);
    }

    [Fact]
    public async Task SatisfyFirstResponse_SetsTimestamp()
    {
        var db = TestHelpers.CreateDbContext();
        var opService = CreateOperationalService(db);

        var conv = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conv);

        var start = DateTime.UtcNow;
        await opService.InitializeSlaAsync(
            TestHelpers.TenantId, conv.Id, ConversationPriority.Normal, start, TestHelpers.UserId1);

        var respondedAt = start.AddMinutes(5);
        await opService.SatisfyFirstResponseAsync(
            TestHelpers.TenantId, conv.Id, respondedAt, TestHelpers.UserId1);

        var sla = await opService.GetSlaStateAsync(TestHelpers.TenantId, conv.Id);

        Assert.NotNull(sla!.FirstResponseAtUtc);
        Assert.False(sla.BreachedFirstResponse);
    }

    [Fact]
    public async Task OperationalSummary_IncludesAllComponents()
    {
        var db = TestHelpers.CreateDbContext();
        var audit = new NoOpAuditPublisher();
        var queueService = CreateQueueService(db, audit);
        var assignmentService = CreateAssignmentService(db, audit);
        var opService = CreateOperationalService(db, audit);

        var queue = await queueService.CreateAsync(
            TestHelpers.TenantId, TestHelpers.UserId1,
            new CreateConversationQueueRequest("Triage", "TRIAGE", null, true));

        var conv = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conv);

        await assignmentService.AssignAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1,
            new AssignConversationRequest(queue.Id, TestHelpers.UserId2, ConversationPriority.High));

        var summary = await opService.GetOperationalSummaryAsync(TestHelpers.TenantId, conv.Id);

        Assert.NotNull(summary);
        Assert.Equal(conv.Id, summary!.ConversationId);
        Assert.NotNull(summary.Queue);
        Assert.Equal("Triage", summary.Queue!.Name);
        Assert.NotNull(summary.Assignment);
        Assert.Equal(TestHelpers.UserId2, summary.Assignment!.AssignedUserId);
        Assert.NotNull(summary.SlaState);
        Assert.Equal(ConversationPriority.High, summary.SlaState!.Priority);
    }

    [Fact]
    public async Task AuditEvents_RecordedForOperations()
    {
        var db = TestHelpers.CreateDbContext();
        var audit = new NoOpAuditPublisher();
        var queueService = CreateQueueService(db, audit);
        var assignmentService = CreateAssignmentService(db, audit);
        var opService = CreateOperationalService(db, audit);

        var queue = await queueService.CreateAsync(
            TestHelpers.TenantId, TestHelpers.UserId1,
            new CreateConversationQueueRequest("Audit", "AUDIT", null, false));

        var conv = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conv);

        await assignmentService.AssignAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1,
            new AssignConversationRequest(queue.Id, TestHelpers.UserId2, ConversationPriority.Normal));

        await opService.UpdatePriorityAsync(
            TestHelpers.TenantId, conv.Id, TestHelpers.UserId1,
            new UpdateConversationPriorityRequest(ConversationPriority.High));

        Assert.Contains(audit.Events, e => e.EventType == "QueueCreated");
        Assert.Contains(audit.Events, e => e.EventType == "ConversationAssigned");
        Assert.Contains(audit.Events, e => e.EventType == "PriorityChanged");
        Assert.True(audit.Events.Count >= 3, $"Expected at least 3 audit events, got {audit.Events.Count}");
    }

    [Fact]
    public async Task LateFirstResponse_SetsBreachFlag()
    {
        var db = TestHelpers.CreateDbContext();
        var opService = CreateOperationalService(db);

        var conv = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conv);

        var start = DateTime.UtcNow.AddHours(-10);
        await opService.InitializeSlaAsync(
            TestHelpers.TenantId, conv.Id, ConversationPriority.Normal, start, TestHelpers.UserId1);

        var respondedAt = DateTime.UtcNow;
        await opService.SatisfyFirstResponseAsync(
            TestHelpers.TenantId, conv.Id, respondedAt, TestHelpers.UserId1);

        var sla = await opService.GetSlaStateAsync(TestHelpers.TenantId, conv.Id);

        Assert.NotNull(sla!.FirstResponseAtUtc);
        Assert.True(sla.BreachedFirstResponse, "First response should be marked as breached when responded after due date");
    }

    [Fact]
    public async Task LateResolution_SetsBreachFlag()
    {
        var db = TestHelpers.CreateDbContext();
        var opService = CreateOperationalService(db);

        var conv = TestHelpers.CreateTestConversation();
        await TestHelpers.CreateConversationRepo(db).AddAsync(conv);

        var start = DateTime.UtcNow.AddHours(-100);
        await opService.InitializeSlaAsync(
            TestHelpers.TenantId, conv.Id, ConversationPriority.Normal, start, TestHelpers.UserId1);

        var resolvedAt = DateTime.UtcNow;
        await opService.SatisfyResolutionAsync(
            TestHelpers.TenantId, conv.Id, resolvedAt, TestHelpers.UserId1);

        var sla = await opService.GetSlaStateAsync(TestHelpers.TenantId, conv.Id);

        Assert.NotNull(sla!.ResolvedAtUtc);
        Assert.True(sla.BreachedResolution, "Resolution should be marked as breached when resolved after due date");
    }
}
