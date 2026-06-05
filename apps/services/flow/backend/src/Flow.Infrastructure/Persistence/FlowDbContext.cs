using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Flow.Infrastructure.Persistence;

public class FlowDbContext : DbContext, IFlowDbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public FlowDbContext(DbContextOptions<FlowDbContext> options) : base(options)
    {
    }

    public FlowDbContext(DbContextOptions<FlowDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<FlowDefinition> FlowDefinitions => Set<FlowDefinition>();
    public DbSet<WorkflowStage> WorkflowStages => Set<WorkflowStage>();
    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<WorkflowAutomationHook> AutomationHooks => Set<WorkflowAutomationHook>();
    public DbSet<AutomationAction> AutomationActions => Set<AutomationAction>();
    public DbSet<AutomationExecutionLog> AutomationExecutionLogs => Set<AutomationExecutionLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ProductWorkflowMapping> ProductWorkflowMappings => Set<ProductWorkflowMapping>();
    // LS-FLOW-MERGE-P4 — dedicated workflow-instance grain.
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    // LS-FLOW-E10.2 — transactional outbox for durable side effects.
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FlowDefinition>(entity =>
        {
            entity.ToTable("flow_definitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(2048);
            entity.Property(e => e.Version).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.ProductKey).IsRequired().HasMaxLength(64).HasDefaultValue(Flow.Domain.Common.ProductKeys.FlowGeneric);
            entity.HasIndex(e => new { e.TenantId, e.ProductKey });
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        modelBuilder.Entity<WorkflowStage>(entity =>
        {
            entity.ToTable("flow_workflow_stages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.MappedStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Order).IsRequired();
            entity.HasIndex(e => new { e.WorkflowDefinitionId, e.Key }).IsUnique();
            entity.HasOne(e => e.WorkflowDefinition)
                .WithMany(w => w.Stages)
                .HasForeignKey(e => e.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        modelBuilder.Entity<WorkflowTransition>(entity =>
        {
            entity.ToTable("flow_workflow_transitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.RulesJson).HasMaxLength(2048);
            entity.HasIndex(e => new { e.WorkflowDefinitionId, e.FromStageId, e.ToStageId }).IsUnique();
            entity.HasOne(e => e.WorkflowDefinition)
                .WithMany(w => w.Transitions)
                .HasForeignKey(e => e.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.FromStage)
                .WithMany(s => s.TransitionsFrom)
                .HasForeignKey(e => e.FromStageId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ToStage)
                .WithMany(s => s.TransitionsTo)
                .HasForeignKey(e => e.ToStageId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        modelBuilder.Entity<WorkflowAutomationHook>(entity =>
        {
            entity.ToTable("flow_automation_hooks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.TriggerEventType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ActionType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ConfigJson).HasMaxLength(2048);
            entity.Property(e => e.ProductKey).IsRequired().HasMaxLength(64).HasDefaultValue(Flow.Domain.Common.ProductKeys.FlowGeneric);
            entity.HasIndex(e => new { e.TenantId, e.ProductKey });
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
            entity.HasIndex(e => new { e.WorkflowDefinitionId, e.WorkflowTransitionId });
            entity.HasOne(e => e.WorkflowDefinition)
                .WithMany(w => w.AutomationHooks)
                .HasForeignKey(e => e.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.WorkflowTransition)
                .WithMany()
                .HasForeignKey(e => e.WorkflowTransitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        modelBuilder.Entity<AutomationAction>(entity =>
        {
            entity.ToTable("flow_automation_actions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.ActionType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ConfigJson).HasMaxLength(2048);
            entity.Property(e => e.ConditionJson).HasMaxLength(2048);
            entity.Property(e => e.Order).IsRequired();
            entity.Property(e => e.RetryCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.RetryDelaySeconds);
            entity.Property(e => e.StopOnFailure).IsRequired().HasDefaultValue(false);
            entity.HasIndex(e => new { e.HookId, e.Order }).IsUnique();
            entity.HasOne(e => e.Hook)
                .WithMany(h => h.Actions)
                .HasForeignKey(e => e.HookId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        modelBuilder.Entity<AutomationExecutionLog>(entity =>
        {
            entity.ToTable("flow_automation_execution_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Message).HasMaxLength(2048);
            entity.Property(e => e.ActionType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ActionOrder);
            // Default 1 matches pre-019-C semantics: every legacy log row
            // represented exactly one execution attempt. The executor always
            // populates Attempts explicitly for new rows.
            entity.Property(e => e.Attempts).IsRequired().HasDefaultValue(1);
            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => e.WorkflowAutomationHookId);
            entity.HasIndex(e => e.ActionId);
            entity.HasOne(e => e.Task)
                .WithMany()
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AutomationHook)
                .WithMany()
                .HasForeignKey(e => e.WorkflowAutomationHookId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("flow_notifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.TargetUserId).HasMaxLength(256);
            entity.Property(e => e.TargetRoleKey).HasMaxLength(128);
            entity.Property(e => e.TargetOrgId).HasMaxLength(256);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(16);
            entity.Property(e => e.SourceType).IsRequired().HasMaxLength(64);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.TargetUserId);
            entity.HasIndex(e => e.TargetRoleKey);
            entity.HasIndex(e => e.TargetOrgId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.TaskId);
            entity.HasOne(e => e.Task)
                .WithMany()
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.WorkflowDefinition)
                .WithMany()
                .HasForeignKey(e => e.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        modelBuilder.Entity<ProductWorkflowMapping>(entity =>
        {
            entity.ToTable("flow_product_workflow_mappings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ProductKey).IsRequired().HasMaxLength(64);
            entity.Property(e => e.SourceEntityType).IsRequired().HasMaxLength(128);
            entity.Property(e => e.SourceEntityId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.CorrelationKey).HasMaxLength(256);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32).HasDefaultValue("Active");
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
            entity.HasIndex(e => new { e.TenantId, e.ProductKey });
            entity.HasIndex(e => new { e.TenantId, e.ProductKey, e.SourceEntityType, e.SourceEntityId })
                .HasDatabaseName("ix_pwm_product_entity");
            entity.HasIndex(e => e.WorkflowDefinitionId);
            entity.HasIndex(e => e.WorkflowInstanceTaskId);
            // LS-FLOW-MERGE-P4 — index on the new canonical pointer.
            entity.HasIndex(e => e.WorkflowInstanceId);
            entity.HasOne(e => e.WorkflowDefinition)
                .WithMany()
                .HasForeignKey(e => e.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.WorkflowInstanceTask)
                .WithMany()
                .HasForeignKey(e => e.WorkflowInstanceTaskId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.WorkflowInstance)
                .WithMany()
                .HasForeignKey(e => e.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        // LS-FLOW-MERGE-P4 — WorkflowInstance grain.
        modelBuilder.Entity<WorkflowInstance>(entity =>
        {
            entity.ToTable("flow_workflow_instances");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ProductKey).IsRequired().HasMaxLength(64).HasDefaultValue(Flow.Domain.Common.ProductKeys.FlowGeneric);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32).HasDefaultValue("Active");
            entity.Property(e => e.CorrelationKey).HasMaxLength(256);
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
            // LS-FLOW-MERGE-P5 — execution-state columns.
            // CurrentStepKey + Status are concurrency tokens so WorkflowEngine
            // mutators (Advance/Complete/Cancel/Fail) issue conditional
            // UPDATEs and surface DbUpdateConcurrencyException on contention.
            entity.Property(e => e.CurrentStepKey).HasMaxLength(64).IsConcurrencyToken();
            entity.Property(e => e.Status).IsConcurrencyToken();
            entity.Property(e => e.AssignedToUserId).HasMaxLength(256);
            entity.Property(e => e.LastErrorMessage).HasMaxLength(2048);
            // LS-FLOW-E10.3 — SLA / timer columns. SlaStatus has a
            // server default so existing rows backfill cleanly to
            // 'OnTrack' on migration; EscalationLevel defaults to 0.
            // DueAt is nullable (not all instances carry a deadline).
            entity.Property(e => e.SlaStatus)
                .IsRequired()
                .HasMaxLength(16)
                .HasDefaultValue(Flow.Domain.Common.WorkflowSlaStatus.OnTrack);
            entity.Property(e => e.EscalationLevel).IsRequired().HasDefaultValue(0);
            entity.HasIndex(e => new { e.TenantId, e.ProductKey });
            entity.HasIndex(e => e.WorkflowDefinitionId);
            entity.HasIndex(e => e.InitialTaskId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CurrentStageId);
            entity.HasIndex(e => e.AssignedToUserId);
            // Evaluator scan key: filter by Status (non-terminal) and DueAt
            // (non-null), then range-scan by DueAt. Composite index keeps
            // that scan cheap as instance count grows.
            entity.HasIndex(e => new { e.Status, e.DueAt })
                .HasDatabaseName("ix_flow_workflow_instances_status_dueat");
            entity.HasIndex(e => e.SlaStatus)
                .HasDatabaseName("ix_flow_workflow_instances_slastatus");
            entity.HasOne(e => e.WorkflowDefinition)
                .WithMany()
                .HasForeignKey(e => e.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.InitialTask)
                .WithMany()
                .HasForeignKey(e => e.InitialTaskId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.CurrentStage)
                .WithMany()
                .HasForeignKey(e => e.CurrentStageId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        // LS-FLOW-E10.2 — transactional outbox. Intentionally NO tenant
        // query filter: the background OutboxProcessor runs without a
        // request-scoped tenant context and must see rows across every
        // tenant. Tenant isolation is enforced at write time (TenantId
        // is populated from the request context in SaveChangesAsync) and
        // at handler time (the handler keys off the row's TenantId when
        // invoking adapters).
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("flow_outbox_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(16).HasDefaultValue(Flow.Domain.Common.OutboxStatus.Pending);
            entity.Property(e => e.PayloadJson).IsRequired().HasColumnType("longtext");
            entity.Property(e => e.AttemptCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.NextAttemptAt).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(2048);
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
            // Polling claim index: worker selects rows where
            // Status='Pending' AND NextAttemptAt <= NOW() ORDER BY NextAttemptAt.
            entity.HasIndex(e => new { e.Status, e.NextAttemptAt })
                .HasDatabaseName("ix_flow_outbox_status_nextattempt");
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.WorkflowInstanceId);
            // No FK to WorkflowInstance: the row must survive tenant /
            // instance cleanup so post-mortem replay/inspection remains
            // possible. WorkflowInstanceId is informational + indexed.
        });

        WorkflowSeedData.Seed(modelBuilder);

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.ToTable("flow_task_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Description).HasMaxLength(4096);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.ProductKey).IsRequired().HasMaxLength(64).HasDefaultValue(Flow.Domain.Common.ProductKeys.FlowGeneric);
            entity.HasIndex(e => new { e.TenantId, e.ProductKey });
            entity.Property(e => e.AssignedToUserId).HasMaxLength(256);
            entity.Property(e => e.AssignedToRoleKey).HasMaxLength(128);
            entity.Property(e => e.AssignedToOrgId).HasMaxLength(256);
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AssignedToUserId);
            entity.HasIndex(e => e.AssignedToRoleKey);
            entity.HasIndex(e => e.AssignedToOrgId);
            entity.HasIndex(e => e.FlowDefinitionId);
            entity.HasIndex(e => e.WorkflowStageId);
            entity.HasOne(e => e.WorkflowDefinition)
                .WithMany()
                .HasForeignKey(e => e.FlowDefinitionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.WorkflowStage)
                .WithMany()
                .HasForeignKey(e => e.WorkflowStageId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.OwnsOne(e => e.Context, ctx =>
            {
                ctx.Property(c => c.ContextType).HasMaxLength(128).HasColumnName("context_type");
                ctx.Property(c => c.ContextId).HasMaxLength(256).HasColumnName("context_id");
                ctx.Property(c => c.Label).HasMaxLength(512).HasColumnName("context_label");
                ctx.HasIndex(c => new { c.ContextType, c.ContextId });
            });
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // LS-FLOW-E10.2 — tenant resolution is best-effort here.
        // ClaimsTenantProvider throws when no HTTP claim is present, which is
        // expected in background scopes (e.g. OutboxProcessor) that update
        // existing rows whose TenantId is already set. The hard tenant
        // assertion below still fires for Added entities without a TenantId.
        string? tenantId = null;
        if (_tenantProvider is not null)
        {
            try { tenantId = _tenantProvider.GetTenantId(); }
            catch (InvalidOperationException) { /* background scope — no claim */ }
        }

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.TenantId))
            {
                if (string.IsNullOrEmpty(tenantId))
                {
                    throw new InvalidOperationException(
                        "Cannot persist new Flow entity: no tenant context available. " +
                        "Authenticated requests must carry a tenant_id claim.");
                }
                entry.Entity.TenantId = tenantId;
            }
        }

        // LS-FLOW-020-A — Defensively normalise ProductKey on Added entities
        // so direct entity construction (e.g. seed data, tests, migrations)
        // never persists an empty value. Service-layer validation runs first
        // for normal request paths.
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added) continue;
            switch (entry.Entity)
            {
                case Flow.Domain.Entities.FlowDefinition fd when string.IsNullOrWhiteSpace(fd.ProductKey):
                    fd.ProductKey = Flow.Domain.Common.ProductKeys.FlowGeneric;
                    break;
                case Flow.Domain.Entities.TaskItem ti when string.IsNullOrWhiteSpace(ti.ProductKey):
                    ti.ProductKey = Flow.Domain.Common.ProductKeys.FlowGeneric;
                    break;
                case Flow.Domain.Entities.WorkflowAutomationHook hk when string.IsNullOrWhiteSpace(hk.ProductKey):
                    hk.ProductKey = Flow.Domain.Common.ProductKeys.FlowGeneric;
                    break;
            }
        }

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> IFlowDbContext.BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return Database.BeginTransactionAsync(cancellationToken);
    }

    Microsoft.EntityFrameworkCore.Storage.IExecutionStrategy IFlowDbContext.CreateExecutionStrategy()
    {
        return Database.CreateExecutionStrategy();
    }
}
