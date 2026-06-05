using BuildingBlocks.Context;
using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;

namespace Reports.Infrastructure.Adapters;

public sealed class ClaimsIdentityAdapter : IIdentityAdapter
{
    private readonly ICurrentRequestContext _ctx;
    private readonly ILogger<ClaimsIdentityAdapter> _log;

    public ClaimsIdentityAdapter(ICurrentRequestContext ctx, ILogger<ClaimsIdentityAdapter> log)
    {
        _ctx = ctx;
        _log = log;
    }

    public Task<AdapterResult<bool>> ValidateTokenAsync(RequestContext ctx, string token, CancellationToken ct)
    {
        var valid = _ctx.IsAuthenticated;
        _log.LogDebug("ClaimsIdentityAdapter: ValidateToken authenticated={IsAuthenticated} [Correlation={CorrelationId}]",
            valid, ctx.CorrelationId);
        return Task.FromResult(AdapterResult<bool>.Ok(valid));
    }

    public Task<AdapterResult<UserContext>> GetUserFromTokenAsync(RequestContext ctx, string token, CancellationToken ct)
    {
        if (!_ctx.IsAuthenticated || _ctx.UserId is null)
        {
            _log.LogWarning("ClaimsIdentityAdapter: Unauthenticated request [Correlation={CorrelationId}]", ctx.CorrelationId);
            return Task.FromResult(AdapterResult<UserContext>.Fail("UNAUTHENTICATED", "No authenticated user in current request context."));
        }

        var user = new UserContext
        {
            UserId = _ctx.UserId.Value.ToString(),
            Email = _ctx.Email,
            Roles = _ctx.Roles.ToList().AsReadOnly(),
            IsPlatformAdmin = _ctx.IsPlatformAdmin,
        };

        _log.LogDebug("ClaimsIdentityAdapter: Resolved user {UserId} [Correlation={CorrelationId}]",
            user.UserId, ctx.CorrelationId);

        return Task.FromResult(AdapterResult<UserContext>.Ok(user));
    }

    public Task<AdapterResult<IReadOnlyList<string>>> GetUserRolesAsync(RequestContext ctx, string userId, CancellationToken ct)
    {
        IReadOnlyList<string> roles = _ctx.Roles.ToList().AsReadOnly();
        _log.LogDebug("ClaimsIdentityAdapter: GetUserRoles for {UserId} returned {RoleCount} roles [Correlation={CorrelationId}]",
            userId, roles.Count, ctx.CorrelationId);
        return Task.FromResult(AdapterResult<IReadOnlyList<string>>.Ok(roles));
    }
}
