// LSCC-01-004: BlockedProviderAccessLog domain entity tests.
using CareConnect.Domain;
using Xunit;

namespace CareConnect.Tests.Domain;

/// <summary>
/// LSCC-01-004 — BlockedProviderAccessLog.Create() factory tests.
///
///   1. Creates with all provided values set correctly
///   2. Id is a non-empty GUID (auto-generated)
///   3. AttemptedAtUtc is a UTC datetime close to now
///   4. UserEmail is lower-cased and trimmed
///   5. Nullable context fields (TenantId, UserId, OrganizationId, ProviderId, ReferralId)
///      accept null without throwing
///   6. FailureReason is stored as-is (no transformation)
///   7. Two calls produce distinct Ids (no shared state)
/// </summary>
public class BlockedProviderAccessLogTests
{
    private static BlockedProviderAccessLog Build(
        Guid?   tenantId       = null,
        Guid?   userId         = null,
        string? userEmail      = null,
        Guid?   organizationId = null,
        Guid?   providerId     = null,
        Guid?   referralId     = null,
        string  failureReason  = "not_provisioned")
    {
        return BlockedProviderAccessLog.Create(
            tenantId:       tenantId,
            userId:         userId,
            userEmail:      userEmail,
            organizationId: organizationId,
            providerId:     providerId,
            referralId:     referralId,
            failureReason:  failureReason);
    }

    [Fact]
    public void Create_AllFieldsProvided_SetsPropertiesCorrectly()
    {
        var tenantId  = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var orgId     = Guid.NewGuid();
        var pId       = Guid.NewGuid();
        var rId       = Guid.NewGuid();

        var log = Build(
            tenantId:       tenantId,
            userId:         userId,
            userEmail:      "Provider@Example.COM",
            organizationId: orgId,
            providerId:     pId,
            referralId:     rId,
            failureReason:  "missing_role");

        Assert.Equal(tenantId,  log.TenantId);
        Assert.Equal(userId,    log.UserId);
        Assert.Equal("provider@example.com", log.UserEmail);   // normalised
        Assert.Equal(orgId,     log.OrganizationId);
        Assert.Equal(pId,       log.ProviderId);
        Assert.Equal(rId,       log.ReferralId);
        Assert.Equal("missing_role", log.FailureReason);
    }

    [Fact]
    public void Create_IdIsNonEmptyGuid()
    {
        var log = Build();
        Assert.NotEqual(Guid.Empty, log.Id);
    }

    [Fact]
    public void Create_AttemptedAtUtc_IsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-2);
        var log    = Build();
        var after  = DateTime.UtcNow.AddSeconds(2);

        Assert.InRange(log.AttemptedAtUtc, before, after);
        Assert.Equal(DateTimeKind.Utc, log.AttemptedAtUtc.Kind);
    }

    [Fact]
    public void Create_UserEmail_IsLowerCasedAndTrimmed()
    {
        var log = Build(userEmail: "  JOHN.DOE@EXAMPLE.COM  ");
        Assert.Equal("john.doe@example.com", log.UserEmail);
    }

    [Fact]
    public void Create_AllNullableContext_DoesNotThrow()
    {
        var ex = Record.Exception(() => Build(
            tenantId:       null,
            userId:         null,
            userEmail:      null,
            organizationId: null,
            providerId:     null,
            referralId:     null));

        Assert.Null(ex);
    }

    [Fact]
    public void Create_NullUserEmail_StoredAsNull()
    {
        var log = Build(userEmail: null);
        Assert.Null(log.UserEmail);
    }

    [Fact]
    public void Create_FailureReason_StoredVerbatim()
    {
        const string reason = "not_provisioned";
        var log = Build(failureReason: reason);
        Assert.Equal(reason, log.FailureReason);
    }

    [Fact]
    public void Create_TwoCalls_ProduceDistinctIds()
    {
        var log1 = Build();
        var log2 = Build();
        Assert.NotEqual(log1.Id, log2.Id);
    }
}
