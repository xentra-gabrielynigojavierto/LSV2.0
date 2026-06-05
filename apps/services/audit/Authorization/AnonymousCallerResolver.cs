namespace PlatformAuditEventService.Authorization;

/// <summary>
/// Caller resolver for <c>QueryAuth:Mode = "None"</c> (development / test only).
///
/// Returns a <see cref="QueryCallerContext"/> with <see cref="CallerScope.Unknown"/>
/// scope so that all requests are rejected by <see cref="Middleware.QueryAuthMiddleware"/>
/// even in dev mode — preventing accidental data exposure during development.
///
/// WARNING: This resolver must never be active in non-development environments.
///          The <c>QueryAuth:Mode</c> setting controls which resolver is registered.
///          In production, <c>Mode</c> must be set to <c>Bearer</c>.
/// </summary>
public sealed class AnonymousCallerResolver : IQueryCallerResolver
{
    public string Mode => "None";

    public Task<IQueryCallerContext> ResolveAsync(HttpContext context, CancellationToken ct = default) =>
        Task.FromResult<IQueryCallerContext>(QueryCallerContext.Anonymous());
}
