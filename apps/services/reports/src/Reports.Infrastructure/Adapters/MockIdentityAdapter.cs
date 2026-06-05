using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;

namespace Reports.Infrastructure.Adapters;

public sealed class MockIdentityAdapter : IIdentityAdapter
{
    private readonly ILogger<MockIdentityAdapter> _log;

    public MockIdentityAdapter(ILogger<MockIdentityAdapter> log) => _log = log;

    public Task<AdapterResult<bool>> ValidateTokenAsync(RequestContext ctx, string token, CancellationToken ct)
    {
        _log.LogDebug("MockIdentityAdapter: ValidateToken [Correlation={CorrelationId}]", ctx.CorrelationId);
        var valid = !string.IsNullOrWhiteSpace(token);
        return Task.FromResult(AdapterResult<bool>.Ok(valid));
    }

    public Task<AdapterResult<UserContext>> GetUserFromTokenAsync(RequestContext ctx, string token, CancellationToken ct)
    {
        _log.LogDebug("MockIdentityAdapter: GetUserFromToken [Correlation={CorrelationId}]", ctx.CorrelationId);
        var user = new UserContext
        {
            UserId = "mock-user-id",
            Email = "mock@example.com",
            Roles = new[] { "reports-viewer", "reports-executor" },
            IsPlatformAdmin = false,
        };
        return Task.FromResult(AdapterResult<UserContext>.Ok(user));
    }

    public Task<AdapterResult<IReadOnlyList<string>>> GetUserRolesAsync(RequestContext ctx, string userId, CancellationToken ct)
    {
        _log.LogDebug("MockIdentityAdapter: GetUserRoles for {UserId} [Correlation={CorrelationId}]", userId, ctx.CorrelationId);
        IReadOnlyList<string> roles = new[] { "reports-viewer", "reports-executor" };
        return Task.FromResult(AdapterResult<IReadOnlyList<string>>.Ok(roles));
    }
}
