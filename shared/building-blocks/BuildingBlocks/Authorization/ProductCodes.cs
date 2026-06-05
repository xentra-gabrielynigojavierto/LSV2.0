namespace BuildingBlocks.Authorization;

public static class ProductCodes
{
    public const string SynqCareConnect = "SYNQ_CARECONNECT";
    public const string SynqFund        = "SYNQ_FUND";
    public const string SynqLiens       = "SYNQ_LIENS";
    public const string SynqPay         = "SYNQ_PAY";
    /// <summary>LS-ID-TNT-010: Synq Insights analytics product.</summary>
    public const string SynqInsights    = "SYNQ_INSIGHTS";
    /// <summary>LS-ID-TNT-010: Synq Comms messaging product.</summary>
    public const string SynqComms       = "SYNQ_COMMS";
    /// <summary>
    /// LS-ID-TNT-011: Virtual pseudo-product code used as the catalog anchor for
    /// tenant-level permission codes (TENANT.*).  Never enabled in TenantProducts;
    /// not a subscribable product for tenants.
    /// </summary>
    public const string SynqPlatform    = "SYNQ_PLATFORM";
}
