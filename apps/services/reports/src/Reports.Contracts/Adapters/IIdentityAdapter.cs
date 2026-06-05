using Reports.Contracts.Context;

namespace Reports.Contracts.Adapters;

public interface IIdentityAdapter
{
    Task<AdapterResult<bool>> ValidateTokenAsync(RequestContext ctx, string token, CancellationToken ct = default);
    Task<AdapterResult<UserContext>> GetUserFromTokenAsync(RequestContext ctx, string token, CancellationToken ct = default);
    Task<AdapterResult<IReadOnlyList<string>>> GetUserRolesAsync(RequestContext ctx, string userId, CancellationToken ct = default);
}
