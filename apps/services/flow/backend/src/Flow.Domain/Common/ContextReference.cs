namespace Flow.Domain.Common;

/// <summary>
/// Generic external entity reference for linking tasks/flows to consuming system entities.
/// Stores type + id pairs without interpreting them — Flow remains decoupled.
/// 
/// FUTURE: Support for multiple/related context references per entity will be
/// introduced in a later bundle (e.g., a separate context_references junction table).
/// The current single-context owned-entity pattern is preserved for simplicity.
/// </summary>
public class ContextReference
{
    public string ContextType { get; set; } = string.Empty;
    public string ContextId { get; set; } = string.Empty;
    public string? Label { get; set; }
}
