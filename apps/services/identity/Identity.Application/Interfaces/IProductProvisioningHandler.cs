using Identity.Domain;

namespace Identity.Application.Interfaces;

public record ProductProvisioningContext(
    Guid TenantId,
    Guid ProductId,
    string ProductCode,
    List<Organization> EligibleOrganizations);

public record ProductProvisioningHandlerResult(
    string ProductCode,
    int OrganizationsProcessed,
    int ProvidersCreated,
    int ProvidersLinked,
    List<string> Warnings);

public interface IProductProvisioningHandler
{
    string ProductCode { get; }

    Task<ProductProvisioningHandlerResult> HandleAsync(
        ProductProvisioningContext context,
        CancellationToken ct = default);
}
