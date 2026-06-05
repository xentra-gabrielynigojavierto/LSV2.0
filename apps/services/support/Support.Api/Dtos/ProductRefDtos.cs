using Support.Api.Domain;

namespace Support.Api.Dtos;

public class CreateProductReferenceRequest
{
    public string ProductCode { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string? DisplayLabel { get; set; }
    public string? MetadataJson { get; set; }
    public string? CreatedByUserId { get; set; }
}

public class ProductReferenceResponse
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string ProductCode { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string? DisplayLabel { get; set; }
    public string? MetadataJson { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public static ProductReferenceResponse From(SupportTicketProductRef r) => new()
    {
        Id = r.Id,
        TicketId = r.TicketId,
        ProductCode = r.ProductCode,
        EntityType = r.EntityType,
        EntityId = r.EntityId,
        DisplayLabel = r.DisplayLabel,
        MetadataJson = r.MetadataJson,
        CreatedByUserId = r.CreatedByUserId,
        CreatedAt = r.CreatedAt,
    };
}
