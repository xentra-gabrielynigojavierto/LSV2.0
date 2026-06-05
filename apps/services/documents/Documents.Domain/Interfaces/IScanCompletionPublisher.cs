using Documents.Domain.Events;

namespace Documents.Domain.Interfaces;

/// <summary>
/// Abstraction for publishing a <see cref="DocumentScanCompletedEvent"/> after a scan
/// reaches a terminal state (Clean, Infected, or Failed).
///
/// Implementations must be non-throwing — all errors are handled internally and
/// surfaced via metrics / logs so the scan pipeline is never interrupted.
/// </summary>
public interface IScanCompletionPublisher
{
    /// <summary>
    /// Publish a scan completion event.
    /// Must catch all delivery exceptions internally; never propagate to the caller.
    /// </summary>
    ValueTask PublishAsync(DocumentScanCompletedEvent evt, CancellationToken ct = default);
}
