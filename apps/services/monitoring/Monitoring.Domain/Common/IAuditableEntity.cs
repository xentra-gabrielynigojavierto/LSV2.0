namespace Monitoring.Domain.Common;

/// <summary>
/// Marker interface for entities whose creation and last-update timestamps are
/// managed automatically by the persistence layer (DbContext SaveChanges
/// interception). Implementations expose timestamp properties with internal
/// setters so the infrastructure can set them while keeping callers out of the
/// audit-stamping concern.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; }
    DateTime UpdatedAtUtc { get; }

    void SetCreatedAt(DateTime utcNow);
    void SetUpdatedAt(DateTime utcNow);
}
