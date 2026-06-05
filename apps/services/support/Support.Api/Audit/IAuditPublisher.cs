namespace Support.Api.Audit;

/// <summary>
/// Abstraction over outbound audit dispatch. Implementations MUST NOT
/// throw on transport failures — Support Service write paths rely on
/// audit dispatch being best-effort.
/// </summary>
public interface IAuditPublisher
{
    Task PublishAsync(SupportAuditEvent auditEvent, CancellationToken ct = default);
}
