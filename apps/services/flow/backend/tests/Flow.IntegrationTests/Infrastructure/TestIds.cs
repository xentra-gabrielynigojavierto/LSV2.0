using Flow.Domain.Common;

namespace Flow.IntegrationTests.Infrastructure;

/// <summary>
/// Stable identifiers used by <see cref="SeedFixture"/> and the test cases.
/// Kept in one place so tests are easy to read against the seed.
///
/// Two tenants × three products × multiple instances cover:
///   - happy-path execution (A→B→Done)
///   - wrong-parent IDOR (right tenant, wrong sourceEntityId)
///   - cross-tenant IDOR (tenant-A caller targeting tenant-B instance)
///   - product correlation (slug/product mismatch)
///   - stale step (CurrentStepKey already advanced)
///   - inactive instance (Status = Completed/Cancelled)
/// </summary>
public static class TestIds
{
    public const string TenantA = "11111111-1111-1111-1111-111111111111";
    public const string TenantB = "22222222-2222-2222-2222-222222222222";

    // ---- Definitions: one per product, per tenant where needed ----------
    public static readonly Guid LienDef_TenantA       = G("aaaa1111-0000-0000-0000-000000000001");
    public static readonly Guid CareConnectDef_TenantA= G("aaaa2222-0000-0000-0000-000000000001");
    public static readonly Guid SynqFundDef_TenantA   = G("aaaa3333-0000-0000-0000-000000000001");
    public static readonly Guid LienDef_TenantB       = G("bbbb1111-0000-0000-0000-000000000001");

    // ---- Stages (per definition, two stages: start → done) --------------
    // Lien tenant-A
    public static readonly Guid LienStageStart_A = G("aaaa1111-0000-0000-0000-000000000010");
    public static readonly Guid LienStageDone_A  = G("aaaa1111-0000-0000-0000-000000000011");
    // CareConnect tenant-A
    public static readonly Guid CcStageStart_A = G("aaaa2222-0000-0000-0000-000000000010");
    public static readonly Guid CcStageDone_A  = G("aaaa2222-0000-0000-0000-000000000011");
    // SynqFund tenant-A
    public static readonly Guid SfStageStart_A = G("aaaa3333-0000-0000-0000-000000000010");
    public static readonly Guid SfStageDone_A  = G("aaaa3333-0000-0000-0000-000000000011");
    // Lien tenant-B
    public static readonly Guid LienStageStart_B = G("bbbb1111-0000-0000-0000-000000000010");
    public static readonly Guid LienStageDone_B  = G("bbbb1111-0000-0000-0000-000000000011");

    // ---- Transitions ----------------------------------------------------
    public static readonly Guid LienTrans_A   = G("aaaa1111-0000-0000-0000-000000000020");
    public static readonly Guid CcTrans_A     = G("aaaa2222-0000-0000-0000-000000000020");
    public static readonly Guid SfTrans_A     = G("aaaa3333-0000-0000-0000-000000000020");
    public static readonly Guid LienTrans_B   = G("bbbb1111-0000-0000-0000-000000000020");

    // ---- Workflow instances --------------------------------------------
    public static readonly Guid HappyLienInstance_A   = G("11110000-0000-0000-0000-000000000001");
    public static readonly Guid HappyCcInstance_A     = G("11110000-0000-0000-0000-000000000002");
    public static readonly Guid HappyFundInstance_A   = G("11110000-0000-0000-0000-000000000003");
    public static readonly Guid CompletedLienInstance_A = G("11110000-0000-0000-0000-0000000000F1");
    public static readonly Guid CrossTenantLienInstance_B = G("22220000-0000-0000-0000-000000000001");

    // ---- Source entities (product-side correlation keys) ----------------
    public const string LienEntityType        = "lien_case";
    public const string LienEntityId_Happy_A  = "lien-case-A-happy";
    public const string LienEntityId_Other_A  = "lien-case-A-other"; // wrong-parent IDOR
    public const string LienEntityId_B        = "lien-case-B";

    public const string CcEntityType   = "referral";
    public const string CcEntityId_A   = "ref-A-happy";

    public const string FundEntityType = "fund_application";
    public const string FundEntityId_A = "app-A-happy";

    // ---- Product slugs the controller accepts ---------------------------
    public const string SlugLien        = "synqlien";
    public const string SlugCareConnect = "careconnect";
    public const string SlugFund        = "synqfund";

    public static readonly string KeyLien        = ProductKeys.SynqLiens;
    public static readonly string KeyCareConnect = ProductKeys.CareConnect;
    public static readonly string KeyFund        = ProductKeys.SynqFund;

    private static Guid G(string s) => Guid.Parse(s);
}
