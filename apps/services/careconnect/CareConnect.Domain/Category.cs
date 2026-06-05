namespace CareConnect.Domain;

public class Category
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public List<ProviderCategory> ProviderCategories { get; private set; } = new();

    private Category() { }
}
