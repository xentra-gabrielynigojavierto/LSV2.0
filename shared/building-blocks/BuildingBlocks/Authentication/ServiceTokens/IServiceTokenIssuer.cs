namespace BuildingBlocks.Authentication.ServiceTokens;

/// <summary>
/// LS-FLOW-MERGE-P5 — issues short-lived HS256 JWTs that product apps
/// attach when calling Flow on behalf of themselves (or on behalf of a
/// user, via the <paramref name="actorUserId"/> claim).
/// </summary>
public interface IServiceTokenIssuer
{
    /// <summary>
    /// Returns true if a signing secret has been configured. Callers
    /// should fall back to the user's bearer when this is false.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Mint a token for the configured service. The <paramref name="tenantId"/>
    /// is required so Flow's tenant filter resolves correctly; the
    /// <paramref name="actorUserId"/> is optional and recorded as the
    /// <c>actor</c> claim for audit (does not change authorization).
    /// </summary>
    string IssueToken(string tenantId, string? actorUserId = null);
}
