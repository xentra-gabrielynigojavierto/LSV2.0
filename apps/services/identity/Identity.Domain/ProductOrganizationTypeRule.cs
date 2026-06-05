namespace Identity.Domain;

/// <summary>
/// Declares which organization types are eligible for a given product role.
/// Replaces the hard-coded EligibleOrgType string on ProductRole.
/// Phase F COMPLETE: EligibleOrgType column dropped (migration 20260330200003).
/// This table is now the sole source of truth for product-role eligibility.
/// Previously both the new rule table and the legacy EligibleOrgType field were checked
/// during the transitional migration window.
/// </summary>
public class ProductOrganizationTypeRule
{
    public Guid Id                { get; private set; }
    public Guid ProductId         { get; private set; }
    public Guid ProductRoleId     { get; private set; }
    public Guid OrganizationTypeId { get; private set; }
    public bool IsActive          { get; private set; }
    public DateTime CreatedAtUtc  { get; private set; }

    public Product          Product          { get; private set; } = null!;
    public ProductRole      ProductRole      { get; private set; } = null!;
    public OrganizationType OrganizationType { get; private set; } = null!;

    private ProductOrganizationTypeRule() { }

    public static ProductOrganizationTypeRule Create(
        Guid productId,
        Guid productRoleId,
        Guid organizationTypeId)
    {
        return new ProductOrganizationTypeRule
        {
            Id                 = Guid.NewGuid(),
            ProductId          = productId,
            ProductRoleId      = productRoleId,
            OrganizationTypeId = organizationTypeId,
            IsActive           = true,
            CreatedAtUtc       = DateTime.UtcNow
        };
    }
}
