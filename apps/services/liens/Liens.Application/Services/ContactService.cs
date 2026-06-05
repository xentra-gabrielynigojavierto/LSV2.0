using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class ContactService : IContactService
{
    private readonly IContactRepository _repo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<ContactService> _logger;

    public ContactService(
        IContactRepository repo,
        IAuditPublisher audit,
        ILogger<ContactService> logger)
    {
        _repo = repo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<PaginatedResult<ContactResponse>> SearchAsync(
        Guid tenantId, string? search, string? contactType, bool? isActive,
        int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _repo.SearchAsync(
            tenantId, search, contactType, isActive, page, pageSize, ct);

        return new PaginatedResult<ContactResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<ContactResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<ContactResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateContactRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.FirstName))
            errors["firstName"] = new[] { "First name is required." };
        if (string.IsNullOrWhiteSpace(request.LastName))
            errors["lastName"] = new[] { "Last name is required." };
        if (!ContactType.All.Contains(request.ContactType))
            errors["contactType"] = new[] { $"Invalid contact type: '{request.ContactType}'. Valid types: {string.Join(", ", ContactType.All)}" };
        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing or invalid.", errors);

        try
        {
            var entity = Contact.Create(
                tenantId, orgId, request.ContactType,
                request.FirstName, request.LastName, actingUserId,
                request.Title, request.Organization,
                request.Email, request.Phone, request.Fax, request.Website,
                request.AddressLine1, request.City, request.State, request.PostalCode,
                request.Notes);

            await _repo.AddAsync(entity, ct);

            _logger.LogInformation(
                "Contact created: {ContactId} {DisplayName} Type={Type} Tenant={TenantId}",
                entity.Id, entity.DisplayName, entity.ContactType, tenantId);

            _audit.Publish(
                eventType: "liens.contact.created",
                action: "create",
                description: $"Contact '{entity.DisplayName}' created",
                tenantId: tenantId,
                actorUserId: actingUserId,
                entityType: "Contact",
                entityId: entity.Id.ToString());

            return MapToResponse(entity);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message,
                new Dictionary<string, string[]> { [ex.ParamName ?? "unknown"] = new[] { ex.Message } });
        }
    }

    public async Task<ContactResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        UpdateContactRequest request, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Contact '{id}' not found for tenant '{tenantId}'.");

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.FirstName))
            errors["firstName"] = new[] { "First name is required." };
        if (string.IsNullOrWhiteSpace(request.LastName))
            errors["lastName"] = new[] { "Last name is required." };
        if (!ContactType.All.Contains(request.ContactType))
            errors["contactType"] = new[] { $"Invalid contact type: '{request.ContactType}'." };
        if (errors.Count > 0)
            throw new ValidationException("One or more fields are invalid.", errors);

        try
        {
            entity.Update(
                request.FirstName, request.LastName, request.ContactType, actingUserId,
                request.Title, request.Organization,
                request.Email, request.Phone, request.Fax, request.Website,
                request.AddressLine1, request.City, request.State, request.PostalCode,
                request.Notes);

            await _repo.UpdateAsync(entity, ct);

            _logger.LogInformation(
                "Contact updated: {ContactId} Tenant={TenantId}", entity.Id, tenantId);

            _audit.Publish(
                eventType: "liens.contact.updated",
                action: "update",
                description: $"Contact '{entity.DisplayName}' updated",
                tenantId: tenantId,
                actorUserId: actingUserId,
                entityType: "Contact",
                entityId: entity.Id.ToString());

            return MapToResponse(entity);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message,
                new Dictionary<string, string[]> { [ex.ParamName ?? "unknown"] = new[] { ex.Message } });
        }
    }

    public async Task<ContactResponse> DeactivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Contact '{id}' not found for tenant '{tenantId}'.");

        entity.Deactivate(actingUserId);
        await _repo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "Contact deactivated: {ContactId} Tenant={TenantId}", entity.Id, tenantId);

        _audit.Publish(
            eventType: "liens.contact.deactivated",
            action: "update",
            description: $"Contact '{entity.DisplayName}' deactivated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Contact",
            entityId: entity.Id.ToString());

        return MapToResponse(entity);
    }

    public async Task<ContactResponse> ReactivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Contact '{id}' not found for tenant '{tenantId}'.");

        entity.Reactivate(actingUserId);
        await _repo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "Contact reactivated: {ContactId} Tenant={TenantId}", entity.Id, tenantId);

        _audit.Publish(
            eventType: "liens.contact.reactivated",
            action: "update",
            description: $"Contact '{entity.DisplayName}' reactivated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Contact",
            entityId: entity.Id.ToString());

        return MapToResponse(entity);
    }

    private static ContactResponse MapToResponse(Contact entity) => new()
    {
        Id = entity.Id,
        ContactType = entity.ContactType,
        FirstName = entity.FirstName,
        LastName = entity.LastName,
        DisplayName = entity.DisplayName,
        Title = entity.Title,
        Organization = entity.Organization,
        Email = entity.Email,
        Phone = entity.Phone,
        Fax = entity.Fax,
        Website = entity.Website,
        AddressLine1 = entity.AddressLine1,
        City = entity.City,
        State = entity.State,
        PostalCode = entity.PostalCode,
        Notes = entity.Notes,
        IsActive = entity.IsActive,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc,
    };
}
