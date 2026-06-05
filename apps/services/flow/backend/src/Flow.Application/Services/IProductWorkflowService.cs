using Flow.Application.DTOs;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-MERGE-P3 — product-facing entry point for instantiating Flow
/// workflows from product business operations and correlating them back to
/// product-side entities. The route layer enforces product-capability authz
/// before this service is ever called.
/// </summary>
public interface IProductWorkflowService
{
    Task<ProductWorkflowResponse> CreateAsync(string productKey, CreateProductWorkflowRequest request, CancellationToken cancellationToken = default);
    Task<List<ProductWorkflowResponse>> ListByProductEntityAsync(string productKey, string sourceEntityType, string sourceEntityId, CancellationToken cancellationToken = default);
    Task<List<ProductWorkflowResponse>> ListByProductAsync(string productKey, CancellationToken cancellationToken = default);
    Task<ProductWorkflowResponse> GetByIdAsync(string productKey, Guid id, CancellationToken cancellationToken = default);
}
