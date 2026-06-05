using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class BillOfSaleService : IBillOfSaleService
{
    private readonly IBillOfSaleRepository _bosRepo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<BillOfSaleService> _logger;

    public BillOfSaleService(
        IBillOfSaleRepository bosRepo,
        IAuditPublisher audit,
        ILogger<BillOfSaleService> logger)
    {
        _bosRepo = bosRepo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<BillOfSaleResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _bosRepo.GetByIdAsync(tenantId, id, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<BillOfSaleResponse?> GetByBillOfSaleNumberAsync(Guid tenantId, string billOfSaleNumber, CancellationToken ct = default)
    {
        var entity = await _bosRepo.GetByBillOfSaleNumberAsync(tenantId, billOfSaleNumber, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<PaginatedResult<BillOfSaleResponse>> SearchAsync(
        Guid tenantId, Guid? lienId, string? status,
        Guid? buyerOrgId, Guid? sellerOrgId,
        string? search,
        int page, int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _bosRepo.SearchAsync(
            tenantId, lienId, status, buyerOrgId, sellerOrgId, search, page, pageSize, ct);

        return new PaginatedResult<BillOfSaleResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<List<BillOfSaleResponse>> GetByLienIdAsync(Guid tenantId, Guid lienId, CancellationToken ct = default)
    {
        var items = await _bosRepo.GetByLienIdAsync(tenantId, lienId, ct);
        return items.Select(MapToResponse).ToList();
    }

    public async Task<BillOfSaleResponse> SubmitForExecutionAsync(
        Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default)
    {
        var entity = await _bosRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"BillOfSale '{id}' not found for tenant '{tenantId}'.");

        try
        {
            entity.SubmitForExecution(actingUserId);
        }
        catch (InvalidOperationException ex)
        {
            throw new ValidationException(ex.Message,
                new Dictionary<string, string[]> { ["status"] = new[] { ex.Message } });
        }

        await _bosRepo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "BOS submitted for execution: {BosId} {BosNumber} Tenant={TenantId}",
            entity.Id, entity.BillOfSaleNumber, tenantId);

        _audit.Publish(
            eventType: "liens.bos.submitted",
            action: "update",
            description: $"BOS '{entity.BillOfSaleNumber}' submitted for execution",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "BillOfSale",
            entityId: entity.Id.ToString());

        return MapToResponse(entity);
    }

    public async Task<BillOfSaleResponse> ExecuteAsync(
        Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default)
    {
        var entity = await _bosRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"BillOfSale '{id}' not found for tenant '{tenantId}'.");

        try
        {
            entity.MarkExecuted(actingUserId);
        }
        catch (InvalidOperationException ex)
        {
            throw new ValidationException(ex.Message,
                new Dictionary<string, string[]> { ["status"] = new[] { ex.Message } });
        }

        await _bosRepo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "BOS executed: {BosId} {BosNumber} Tenant={TenantId}",
            entity.Id, entity.BillOfSaleNumber, tenantId);

        _audit.Publish(
            eventType: "liens.bos.executed",
            action: "update",
            description: $"BOS '{entity.BillOfSaleNumber}' executed",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "BillOfSale",
            entityId: entity.Id.ToString());

        return MapToResponse(entity);
    }

    public async Task<BillOfSaleResponse> CancelAsync(
        Guid tenantId, Guid id, Guid actingUserId, string? reason = null, CancellationToken ct = default)
    {
        var entity = await _bosRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"BillOfSale '{id}' not found for tenant '{tenantId}'.");

        try
        {
            entity.Cancel(actingUserId, reason);
        }
        catch (InvalidOperationException ex)
        {
            throw new ValidationException(ex.Message,
                new Dictionary<string, string[]> { ["status"] = new[] { ex.Message } });
        }

        await _bosRepo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "BOS cancelled: {BosId} {BosNumber} Tenant={TenantId}",
            entity.Id, entity.BillOfSaleNumber, tenantId);

        _audit.Publish(
            eventType: "liens.bos.cancelled",
            action: "update",
            description: $"BOS '{entity.BillOfSaleNumber}' cancelled",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "BillOfSale",
            entityId: entity.Id.ToString());

        return MapToResponse(entity);
    }

    private static BillOfSaleResponse MapToResponse(BillOfSale entity)
    {
        return new BillOfSaleResponse
        {
            Id = entity.Id,
            BillOfSaleNumber = entity.BillOfSaleNumber,
            ExternalReference = entity.ExternalReference,
            Status = entity.Status,
            LienId = entity.LienId,
            LienOfferId = entity.LienOfferId,
            SellerOrgId = entity.SellerOrgId,
            BuyerOrgId = entity.BuyerOrgId,
            PurchaseAmount = entity.PurchaseAmount,
            OriginalLienAmount = entity.OriginalLienAmount,
            DiscountPercent = entity.DiscountPercent,
            SellerContactName = entity.SellerContactName,
            BuyerContactName = entity.BuyerContactName,
            Terms = entity.Terms,
            Notes = entity.Notes,
            DocumentId = entity.DocumentId,
            IssuedAtUtc = entity.IssuedAtUtc,
            ExecutedAtUtc = entity.ExecutedAtUtc,
            EffectiveAtUtc = entity.EffectiveAtUtc,
            CancelledAtUtc = entity.CancelledAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
        };
    }
}
