namespace Identity.Domain;

/// <summary>
/// Declares which relationship types are valid within the context of a product.
/// Enables per-product graph constraints (e.g., only REFERS_TO is valid for CareConnect).
/// </summary>
public class ProductRelationshipTypeRule
{
    public Guid Id                { get; private set; }
    public Guid ProductId         { get; private set; }
    public Guid RelationshipTypeId { get; private set; }
    public bool IsActive          { get; private set; }
    public DateTime CreatedAtUtc  { get; private set; }

    public Product          Product          { get; private set; } = null!;
    public RelationshipType RelationshipType { get; private set; } = null!;

    private ProductRelationshipTypeRule() { }

    public static ProductRelationshipTypeRule Create(Guid productId, Guid relationshipTypeId)
    {
        return new ProductRelationshipTypeRule
        {
            Id                = Guid.NewGuid(),
            ProductId         = productId,
            RelationshipTypeId = relationshipTypeId,
            IsActive          = true,
            CreatedAtUtc      = DateTime.UtcNow
        };
    }
}
