using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class ProductProvisioningService : IProductProvisioningService
{
    private readonly IdentityDbContext _db;
    private readonly IEnumerable<IProductProvisioningHandler> _handlers;
    private readonly ILogger<ProductProvisioningService> _logger;

    public ProductProvisioningService(
        IdentityDbContext db,
        IEnumerable<IProductProvisioningHandler> handlers,
        ILogger<ProductProvisioningService> logger)
    {
        _db = db;
        _handlers = handlers;
        _logger = logger;
    }

    public async Task<ProvisionProductResult> ProvisionAsync(
        ProvisionProductRequest request,
        CancellationToken ct = default)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Code == request.ProductCode, ct)
            ?? throw new InvalidOperationException($"Product '{request.ProductCode}' not found.");

        var tenant = await _db.Tenants
            .Include(t => t.TenantProducts)
                .ThenInclude(tp => tp.Product)
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, ct)
            ?? throw new InvalidOperationException($"Tenant '{request.TenantId}' not found.");

        bool tpCreated = await ProvisionTenantProduct(tenant, product, request.Enabled, ct);

        var (orgCreated, orgUpdated, eligibleOrgs) =
            await ProvisionOrganizationProducts(request.TenantId, product, request.Enabled, ct);

        await _db.SaveChangesAsync(ct);

        ProductProvisioningHandlerResult? handlerResult = null;
        if (request.Enabled && eligibleOrgs.Count > 0)
        {
            handlerResult = await ExecuteProvisioningHandlers(
                request.TenantId, product.Id, request.ProductCode, eligibleOrgs, ct);
        }

        _logger.LogInformation(
            "Product provisioning complete: Tenant={TenantId}, Product={ProductCode}, Enabled={Enabled}, " +
            "TenantProductCreated={TpCreated}, OrgProductsCreated={OrgCreated}, OrgProductsUpdated={OrgUpdated}",
            request.TenantId, request.ProductCode, request.Enabled, tpCreated, orgCreated, orgUpdated);

        return new ProvisionProductResult(
            request.TenantId,
            request.ProductCode,
            request.Enabled,
            tpCreated,
            orgCreated,
            orgUpdated,
            handlerResult);
    }

    private async Task<bool> ProvisionTenantProduct(
        Tenant tenant, Product product, bool enabled, CancellationToken ct)
    {
        var existing = tenant.TenantProducts.FirstOrDefault(tp => tp.ProductId == product.Id);

        if (existing is null)
        {
            if (enabled)
            {
                var tp = TenantProduct.Create(tenant.Id, product.Id);
                _db.Set<TenantProduct>().Add(tp);
                return true;
            }
            return false;
        }

        if (!enabled && existing.IsEnabled)
        {
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE idt_TenantProducts SET IsEnabled = 0 WHERE TenantId = {0} AND ProductId = {1}",
                tenant.Id, product.Id);
            return false;
        }

        if (enabled && !existing.IsEnabled)
        {
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE idt_TenantProducts SET IsEnabled = 1, EnabledAtUtc = {0} WHERE TenantId = {1} AND ProductId = {2}",
                DateTime.UtcNow, tenant.Id, product.Id);
            return true;
        }

        return false;
    }

    private async Task<(int Created, int Updated, List<Organization> EligibleOrgs)>
        ProvisionOrganizationProducts(
            Guid tenantId, Product product, bool enabled, CancellationToken ct)
    {
        var tenantOrgs = await _db.Organizations
            .Include(o => o.OrganizationProducts)
            .Where(o => o.TenantId == tenantId && o.IsActive)
            .ToListAsync(ct);

        int created = 0;
        int updated = 0;
        var eligibleOrgs = new List<Organization>();

        foreach (var org in tenantOrgs)
        {
            var orgProduct = org.OrganizationProducts
                .FirstOrDefault(op => op.ProductId == product.Id);

            if (enabled)
            {
                if (!ProductEligibilityConfig.IsEligible(org.OrgType, product.Code))
                {
                    _logger.LogDebug(
                        "Skipping org {OrgId} ({OrgType}): not eligible for {ProductCode}",
                        org.Id, org.OrgType, product.Code);
                    continue;
                }

                eligibleOrgs.Add(org);

                if (orgProduct is null)
                {
                    _db.OrganizationProducts.Add(
                        OrganizationProduct.Create(org.Id, product.Id));
                    created++;
                }
                else if (!orgProduct.IsEnabled)
                {
                    orgProduct.Enable();
                    updated++;
                }
            }
            else
            {
                if (orgProduct is not null && orgProduct.IsEnabled)
                {
                    orgProduct.Disable();
                    updated++;
                }
            }
        }

        return (created, updated, eligibleOrgs);
    }

    private async Task<ProductProvisioningHandlerResult?> ExecuteProvisioningHandlers(
        Guid tenantId, Guid productId, string productCode,
        List<Organization> eligibleOrgs, CancellationToken ct)
    {
        var handler = _handlers.FirstOrDefault(
            h => string.Equals(h.ProductCode, productCode, StringComparison.OrdinalIgnoreCase));

        if (handler is null)
        {
            _logger.LogDebug("No provisioning handler registered for product {ProductCode}", productCode);
            return null;
        }

        try
        {
            var context = new ProductProvisioningContext(tenantId, productId, productCode, eligibleOrgs);
            var result = await handler.HandleAsync(context, ct);

            _logger.LogInformation(
                "Product handler {ProductCode} completed: Processed={Processed}, Created={Created}, Linked={Linked}, Warnings={WarningCount}",
                productCode, result.OrganizationsProcessed, result.ProvidersCreated,
                result.ProvidersLinked, result.Warnings.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Product handler {ProductCode} failed for tenant {TenantId}. " +
                "Identity-side provisioning is complete; product-specific setup may be incomplete.",
                productCode, tenantId);

            return new ProductProvisioningHandlerResult(
                productCode,
                eligibleOrgs.Count,
                ProvidersCreated: 0,
                ProvidersLinked: 0,
                Warnings: [$"Handler failed: {ex.Message}"]);
        }
    }
}
