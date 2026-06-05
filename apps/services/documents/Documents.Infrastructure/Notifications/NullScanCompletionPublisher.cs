using Documents.Domain.Events;
using Documents.Domain.Interfaces;

namespace Documents.Infrastructure.Notifications;

/// <summary>
/// No-op publisher — silently discards all scan completion events.
/// Used when Notifications:ScanCompletion:Provider=none.
/// </summary>
public sealed class NullScanCompletionPublisher : IScanCompletionPublisher
{
    public ValueTask PublishAsync(DocumentScanCompletedEvent evt, CancellationToken ct = default)
        => ValueTask.CompletedTask;
}
