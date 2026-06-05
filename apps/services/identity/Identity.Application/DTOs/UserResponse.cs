namespace Identity.Application.DTOs;

public record UserResponse(
    Guid Id,
    Guid TenantId,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    List<string> Roles,
    Guid? OrganizationId = null,
    string? OrgType = null,
    List<string>? ProductRoles = null,
    Guid? AvatarDocumentId = null);
