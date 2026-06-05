namespace Support.Api.Notifications;

/// <summary>
/// Resolves user email addresses from the identity database.
/// Used at notification-publish time so that admin, assigned-user, and
/// requester recipients can be addressed by email without requiring the
/// caller to hold the address at the point of ticket creation.
/// </summary>
public interface IUserEmailResolver
{
    /// <summary>
    /// Returns the email address for <paramref name="userId"/> within
    /// <paramref name="tenantId"/>, or <c>null</c> when the user is not found
    /// or the identity DB is unavailable.  Failures are swallowed.
    /// </summary>
    Task<string?> ResolveAsync(string userId, string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Batch-resolves emails for a set of user IDs scoped to a single tenant.
    /// Returns a dictionary keyed by userId; absent entries mean "not found".
    /// </summary>
    Task<Dictionary<string, string>> ResolveManyAsync(IEnumerable<string> userIds, string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns the email addresses of all active, non-locked platform-internal
    /// users (control-centre admins).  Used to BCC every support notification
    /// to the full platform admin group.
    /// Failures are swallowed — an unreachable identity DB must never break
    /// ticket operations.
    /// </summary>
    Task<List<string>> ResolvePlatformAdminEmailsAsync(CancellationToken ct = default);
}
