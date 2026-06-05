using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Flow.Infrastructure.Persistence;

public static class WorkflowSeedData
{
    public static readonly Guid StandardWorkflowId = new("10000000-0000-0000-0000-000000000001");
    public const string DefaultTenantId = "default";

    private static readonly Guid StageOpen = new("20000000-0000-0000-0000-000000000001");
    private static readonly Guid StageInProgress = new("20000000-0000-0000-0000-000000000002");
    private static readonly Guid StageBlocked = new("20000000-0000-0000-0000-000000000003");
    private static readonly Guid StageDone = new("20000000-0000-0000-0000-000000000004");
    private static readonly Guid StageCancelled = new("20000000-0000-0000-0000-000000000005");

    public static void Seed(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FlowDefinition>().HasData(new
        {
            Id = StandardWorkflowId,
            TenantId = DefaultTenantId,
            Name = "Standard Task Flow",
            Description = "Default workflow with standard task lifecycle stages and transitions.",
            Version = "1.0",
            Status = FlowStatus.Active,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = "system"
        });

        modelBuilder.Entity<WorkflowStage>().HasData(
            new { Id = StageOpen, TenantId = DefaultTenantId, WorkflowDefinitionId = StandardWorkflowId, Key = "open", Name = "Open", MappedStatus = TaskItemStatus.Open, Order = 1, IsInitial = true, IsTerminal = false },
            new { Id = StageInProgress, TenantId = DefaultTenantId, WorkflowDefinitionId = StandardWorkflowId, Key = "in-progress", Name = "In Progress", MappedStatus = TaskItemStatus.InProgress, Order = 2, IsInitial = false, IsTerminal = false },
            new { Id = StageBlocked, TenantId = DefaultTenantId, WorkflowDefinitionId = StandardWorkflowId, Key = "blocked", Name = "Blocked", MappedStatus = TaskItemStatus.Blocked, Order = 3, IsInitial = false, IsTerminal = false },
            new { Id = StageDone, TenantId = DefaultTenantId, WorkflowDefinitionId = StandardWorkflowId, Key = "done", Name = "Done", MappedStatus = TaskItemStatus.Done, Order = 4, IsInitial = false, IsTerminal = true },
            new { Id = StageCancelled, TenantId = DefaultTenantId, WorkflowDefinitionId = StandardWorkflowId, Key = "cancelled", Name = "Cancelled", MappedStatus = TaskItemStatus.Cancelled, Order = 5, IsInitial = false, IsTerminal = true }
        );

        var transitions = new[]
        {
            (From: StageOpen, To: StageInProgress, Name: "Start Work"),
            (From: StageOpen, To: StageCancelled, Name: "Cancel"),
            (From: StageInProgress, To: StageBlocked, Name: "Block"),
            (From: StageInProgress, To: StageDone, Name: "Complete"),
            (From: StageInProgress, To: StageCancelled, Name: "Cancel"),
            (From: StageBlocked, To: StageInProgress, Name: "Unblock"),
            (From: StageBlocked, To: StageCancelled, Name: "Cancel"),
            (From: StageDone, To: StageOpen, Name: "Reopen"),
            (From: StageCancelled, To: StageOpen, Name: "Reopen"),
        };

        int i = 1;
        foreach (var t in transitions)
        {
            modelBuilder.Entity<WorkflowTransition>().HasData(new
            {
                Id = new Guid($"30000000-0000-0000-0000-{i:D12}"),
                TenantId = DefaultTenantId,
                WorkflowDefinitionId = StandardWorkflowId,
                FromStageId = t.From,
                ToStageId = t.To,
                Name = t.Name,
                IsActive = true
            });
            i++;
        }
    }
}
