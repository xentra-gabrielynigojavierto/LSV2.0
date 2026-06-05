using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface IContactService
{
    Task<PaginatedResult<ContactResponse>> SearchAsync(
        Guid tenantId, string? search, string? contactType, bool? isActive,
        int page, int pageSize, CancellationToken ct = default);

    Task<ContactResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<ContactResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateContactRequest request, CancellationToken ct = default);

    Task<ContactResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        UpdateContactRequest request, CancellationToken ct = default);

    Task<ContactResponse> DeactivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default);

    Task<ContactResponse> ReactivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default);
}
