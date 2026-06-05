namespace Tenant.Application.Interfaces;

/// <summary>
/// Adapter for calling the Documents service from the Tenant service.
/// All operations are non-fatal: failures are logged as warnings and do not
/// roll back the Tenant DB write that preceded them.
/// </summary>
public interface IDocumentsAdapter
{
    /// <summary>
    /// Marks <paramref name="documentId"/> as the published logo in the Documents service
    /// (sets IsPublishedAsLogo = true) so that the anonymous
    /// <c>GET /public/logo/{id}</c> endpoint can serve it.
    /// </summary>
    Task RegisterLogoAsync(
        Guid              documentId,
        Guid              tenantId,
        string?           authHeader,
        CancellationToken ct = default);

    /// <summary>
    /// Clears all logo registrations for <paramref name="tenantId"/> in the Documents service
    /// so the anonymous public logo endpoint stops serving the old logo.
    /// </summary>
    Task DeregisterLogoAsync(
        Guid              tenantId,
        string?           authHeader,
        CancellationToken ct = default);
}
