namespace Identity.Application.Interfaces;

public record ProvisionProductRequest(
    Guid TenantId,
    string ProductCode,
    bool Enabled);

public record ProvisionProductResult(
    Guid TenantId,
    string ProductCode,
    bool Enabled,
    bool TenantProductCreated,
    int OrganizationProductsCreated,
    int OrganizationProductsUpdated,
    ProductProvisioningHandlerResult? HandlerResult);

public interface IProductProvisioningService
{
    Task<ProvisionProductResult> ProvisionAsync(
        ProvisionProductRequest request,
        CancellationToken ct = default);
}
