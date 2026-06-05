using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Flow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flow.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit class fixture that owns one <see cref="FlowApiFactory"/> per test
/// class and seeds a deterministic dataset:
///
///   - 3 product definitions in tenant A (SynqLien, CareConnect, SynqFund),
///     each with two stages (start → done) and one transition.
///   - 1 SynqLien definition in tenant B (cross-tenant IDOR target).
///   - 1 active instance per tenant-A product mapped to a product entity.
///   - 1 already-completed Lien instance in tenant A (inactive scenarios).
///   - 1 active Lien instance in tenant B (cross-tenant scenarios).
///
/// Seeding bypasses the tenant query filter and the SaveChangesAsync tenant
/// guard by setting <see cref="BaseEntity.TenantId"/> explicitly on every
/// row, then opening a context whose <c>ITenantProvider</c> sees no request
/// (so the soft fallback returns empty for excluded paths only — but
/// SaveChanges only requires TenantId to be present, which we provide).
/// </summary>
public sealed class SeedFixture : IDisposable
{
    public FlowApiFactory Factory { get; }

    public SeedFixture()
    {
        Factory = new FlowApiFactory();
        // Force the host to build so the DbContext registration is wired up.
        _ = Factory.Services;
        Seed();
    }

    private void Seed()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowDbContext>();

        // ---- Definitions ------------------------------------------------
        db.FlowDefinitions.AddRange(
            Def(TestIds.LienDef_TenantA, TestIds.TenantA, "Lien Sale Flow",        ProductKeys.SynqLiens),
            Def(TestIds.CareConnectDef_TenantA, TestIds.TenantA, "Referral Flow",  ProductKeys.CareConnect),
            Def(TestIds.SynqFundDef_TenantA, TestIds.TenantA, "Fund Application",  ProductKeys.SynqFund),
            Def(TestIds.LienDef_TenantB, TestIds.TenantB, "Lien Sale Flow (B)",    ProductKeys.SynqLiens)
        );

        // ---- Stages -----------------------------------------------------
        db.WorkflowStages.AddRange(
            Stage(TestIds.LienStageStart_A, TestIds.TenantA, TestIds.LienDef_TenantA, "start", isInitial: true),
            Stage(TestIds.LienStageDone_A,  TestIds.TenantA, TestIds.LienDef_TenantA, "done",  isInitial: false, isTerminal: true, order: 2),
            Stage(TestIds.CcStageStart_A, TestIds.TenantA, TestIds.CareConnectDef_TenantA, "start", isInitial: true),
            Stage(TestIds.CcStageDone_A,  TestIds.TenantA, TestIds.CareConnectDef_TenantA, "done",  isInitial: false, isTerminal: true, order: 2),
            Stage(TestIds.SfStageStart_A, TestIds.TenantA, TestIds.SynqFundDef_TenantA, "start", isInitial: true),
            Stage(TestIds.SfStageDone_A,  TestIds.TenantA, TestIds.SynqFundDef_TenantA, "done",  isInitial: false, isTerminal: true, order: 2),
            Stage(TestIds.LienStageStart_B, TestIds.TenantB, TestIds.LienDef_TenantB, "start", isInitial: true),
            Stage(TestIds.LienStageDone_B,  TestIds.TenantB, TestIds.LienDef_TenantB, "done",  isInitial: false, isTerminal: true, order: 2)
        );

        // ---- Transitions -----------------------------------------------
        db.WorkflowTransitions.AddRange(
            Trans(TestIds.LienTrans_A, TestIds.TenantA, TestIds.LienDef_TenantA, TestIds.LienStageStart_A, TestIds.LienStageDone_A),
            Trans(TestIds.CcTrans_A,   TestIds.TenantA, TestIds.CareConnectDef_TenantA, TestIds.CcStageStart_A, TestIds.CcStageDone_A),
            Trans(TestIds.SfTrans_A,   TestIds.TenantA, TestIds.SynqFundDef_TenantA, TestIds.SfStageStart_A, TestIds.SfStageDone_A),
            Trans(TestIds.LienTrans_B, TestIds.TenantB, TestIds.LienDef_TenantB, TestIds.LienStageStart_B, TestIds.LienStageDone_B)
        );

        // ---- Active instances -------------------------------------------
        db.WorkflowInstances.AddRange(
            Inst(TestIds.HappyLienInstance_A, TestIds.TenantA, TestIds.LienDef_TenantA,        ProductKeys.SynqLiens,   TestIds.LienStageStart_A, "start"),
            Inst(TestIds.HappyCcInstance_A,   TestIds.TenantA, TestIds.CareConnectDef_TenantA, ProductKeys.CareConnect, TestIds.CcStageStart_A,  "start"),
            Inst(TestIds.HappyFundInstance_A, TestIds.TenantA, TestIds.SynqFundDef_TenantA,    ProductKeys.SynqFund,    TestIds.SfStageStart_A,  "start"),
            Inst(TestIds.CompletedLienInstance_A, TestIds.TenantA, TestIds.LienDef_TenantA,    ProductKeys.SynqLiens,   TestIds.LienStageDone_A, "done", "Completed"),
            Inst(TestIds.CrossTenantLienInstance_B, TestIds.TenantB, TestIds.LienDef_TenantB,  ProductKeys.SynqLiens,   TestIds.LienStageStart_B,"start")
        );

        // ---- Mappings (parent / product correlation) --------------------
        db.ProductWorkflowMappings.AddRange(
            Map(TestIds.TenantA, ProductKeys.SynqLiens,   TestIds.LienEntityType, TestIds.LienEntityId_Happy_A, TestIds.LienDef_TenantA, TestIds.HappyLienInstance_A),
            Map(TestIds.TenantA, ProductKeys.SynqLiens,   TestIds.LienEntityType, TestIds.LienEntityId_Other_A, TestIds.LienDef_TenantA, instanceId: null),
            Map(TestIds.TenantA, ProductKeys.SynqLiens,   TestIds.LienEntityType, "decoy-completed",            TestIds.LienDef_TenantA, TestIds.CompletedLienInstance_A),
            Map(TestIds.TenantA, ProductKeys.CareConnect, TestIds.CcEntityType,   TestIds.CcEntityId_A,         TestIds.CareConnectDef_TenantA, TestIds.HappyCcInstance_A),
            Map(TestIds.TenantA, ProductKeys.SynqFund,    TestIds.FundEntityType, TestIds.FundEntityId_A,       TestIds.SynqFundDef_TenantA, TestIds.HappyFundInstance_A),
            Map(TestIds.TenantB, ProductKeys.SynqLiens,   TestIds.LienEntityType, TestIds.LienEntityId_B,       TestIds.LienDef_TenantB, TestIds.CrossTenantLienInstance_B)
        );

        db.SaveChanges();
    }

    public void Dispose() => Factory.Dispose();

    // ---- Builders -------------------------------------------------------

    private static FlowDefinition Def(Guid id, string tenantId, string name, string productKey) => new()
    {
        Id = id,
        TenantId = tenantId,
        Name = name,
        Description = name,
        Version = "1.0",
        Status = FlowStatus.Active,
        ProductKey = productKey,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = "seed",
    };

    private static WorkflowStage Stage(Guid id, string tenantId, Guid defId, string key,
        bool isInitial = false, bool isTerminal = false, int order = 1) => new()
    {
        Id = id,
        TenantId = tenantId,
        WorkflowDefinitionId = defId,
        Key = key,
        Name = key,
        MappedStatus = isTerminal ? TaskItemStatus.Done : TaskItemStatus.Open,
        Order = order,
        IsInitial = isInitial,
        IsTerminal = isTerminal,
    };

    private static WorkflowTransition Trans(Guid id, string tenantId, Guid defId, Guid from, Guid to) => new()
    {
        Id = id,
        TenantId = tenantId,
        WorkflowDefinitionId = defId,
        FromStageId = from,
        ToStageId = to,
        Name = "advance",
        IsActive = true,
    };

    private static WorkflowInstance Inst(Guid id, string tenantId, Guid defId, string productKey,
        Guid stageId, string stepKey, string status = "Active") => new()
    {
        Id = id,
        TenantId = tenantId,
        WorkflowDefinitionId = defId,
        ProductKey = productKey,
        Status = status,
        CurrentStageId = stageId,
        CurrentStepKey = stepKey,
        StartedAt = DateTime.UtcNow.AddMinutes(-5),
        CompletedAt = status == "Completed" ? DateTime.UtcNow.AddMinutes(-1) : (DateTime?)null,
        CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        CreatedBy = "seed",
    };

    private static ProductWorkflowMapping Map(string tenantId, string productKey, string entityType,
        string entityId, Guid defId, Guid? instanceId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        ProductKey = productKey,
        SourceEntityType = entityType,
        SourceEntityId = entityId,
        WorkflowDefinitionId = defId,
        WorkflowInstanceId = instanceId,
        Status = instanceId is null ? "Pending" : "Active",
        CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        CreatedBy = "seed",
    };
}
