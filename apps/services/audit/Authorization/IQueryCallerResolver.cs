namespace PlatformAuditEventService.Authorization;

/// <summary>
/// Resolves an <see cref="IQueryCallerContext"/> from the incoming HTTP request.
///
/// Implementations translate identity-provider-specific representations
/// (JWT claims, API keys, mTLS certificates, etc.) into the abstract
/// <see cref="IQueryCallerContext"/> used by the authorization layer.
///
/// Registered as a singleton — implementations must be thread-safe and stateless.
///
/// Extension path:
///   To support a new identity provider, implement this interface.
///   Register the new implementation and update the factory switch in Program.cs.
///   The middleware, authorizer, and controllers require no changes.
/// </summary>
public interface IQueryCallerResolver
{
    /// <summary>
    /// The <c>QueryAuth:Mode</c> value this resolver handles (e.g. "None", "Bearer").
    /// </summary>
    string Mode { get; }

    /// <summary>
    /// Resolve the caller context from <paramref name="context"/>.
    ///
    /// Must never throw — return an <see cref="IQueryCallerContext"/> with
    /// <see cref="IQueryCallerContext.Scope"/> = <see cref="CallerScope.Unknown"/>
    /// if resolution fails. The middleware will convert that to a 401.
    /// </summary>
    Task<IQueryCallerContext> ResolveAsync(HttpContext context, CancellationToken ct = default);
}
