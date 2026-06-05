using Documents.Domain.ValueObjects;

namespace Documents.Domain.Interfaces;

public interface IAccessTokenStore
{
    Task StoreAsync(AccessToken token, CancellationToken ct = default);
    Task<AccessToken?> GetAsync(string tokenString, CancellationToken ct = default);

    /// <summary>Atomically mark token as used. Returns true if this call was the first use.</summary>
    Task<bool> MarkUsedAsync(string tokenString, CancellationToken ct = default);

    Task RevokeAsync(string tokenString, CancellationToken ct = default);
}
