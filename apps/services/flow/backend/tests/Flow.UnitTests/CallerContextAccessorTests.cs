using System.Security.Claims;
using BuildingBlocks.Authentication.ServiceTokens;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Flow.UnitTests;

/// <summary>
/// LS-FLOW-HARDEN-A1 — verifies that the caller-classification logic
/// the new ProductWorkflowExecutionController depends on correctly
/// distinguishes user vs service vs service-on-behalf-of-user, and
/// gracefully reports anonymous as <see cref="CallerType.Unknown"/>.
/// </summary>
public class CallerContextAccessorTests
{
    [Fact]
    public void Anonymous_request_returns_Unknown()
    {
        var sut = Build(new HttpContextAccessor());
        var ctx = sut.Current;

        Assert.Equal(CallerType.Unknown, ctx.Type);
        Assert.Null(ctx.TenantId);
        Assert.Null(ctx.Subject);
    }

    [Fact]
    public void Service_token_with_tenant_resolves_as_Service()
    {
        var sut = WithPrincipal(
            new Claim("sub", "service:liens"),
            new Claim("tenant_id", "tenant-aaa")
        );

        var ctx = sut.Current;
        Assert.Equal(CallerType.Service, ctx.Type);
        Assert.Equal("tenant-aaa", ctx.TenantId);
        Assert.Equal("service:liens", ctx.Subject);
        Assert.Null(ctx.Actor);
        Assert.True(ctx.IsService);
        Assert.False(ctx.IsUser);
    }

    [Fact]
    public void Service_token_with_actor_resolves_as_ServiceOnBehalfOfUser()
    {
        var sut = WithPrincipal(
            new Claim("sub", "service:careconnect"),
            new Claim("tenant_id", "tenant-bbb"),
            new Claim(ServiceTokenAuthenticationDefaults.ActorClaim, "user-42")
        );

        var ctx = sut.Current;
        Assert.Equal(CallerType.ServiceOnBehalfOfUser, ctx.Type);
        Assert.Equal("user-42", ctx.Actor);
        Assert.True(ctx.IsService);
    }

    [Fact]
    public void User_token_resolves_as_User()
    {
        var sut = WithPrincipal(
            new Claim("sub", "user-99"),
            new Claim("tenant_id", "tenant-ccc")
        );

        var ctx = sut.Current;
        Assert.Equal(CallerType.User, ctx.Type);
        Assert.Equal("tenant-ccc", ctx.TenantId);
        Assert.True(ctx.IsUser);
        Assert.False(ctx.IsService);
    }

    [Fact]
    public void Tid_claim_is_accepted_when_tenant_id_missing()
    {
        var sut = WithPrincipal(
            new Claim("sub", "service:fund"),
            new Claim("tid", "tenant-ddd")
        );

        Assert.Equal("tenant-ddd", sut.Current.TenantId);
    }

    // ---------------- helpers ----------------

    private static ICallerContextAccessor Build(IHttpContextAccessor http) =>
        new CallerContextAccessor(http);

    private static ICallerContextAccessor WithPrincipal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return Build(http);
    }
}
