namespace CareConnect.Domain;

public class ProviderCategory
{
    public Guid ProviderId { get; set; }
    public Guid CategoryId { get; set; }

    public Provider? Provider { get; set; }
    public Category? Category { get; set; }
}
